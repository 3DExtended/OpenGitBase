using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Api.Services;

public sealed class PromotePrimaryReplicaQueryHandler
    : IQueryHandler<PromotePrimaryReplicaQuery, PromotePrimaryReplicaResult>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IColdRecoveryService _coldRecoveryService;
    private readonly ILfsRepairQueue _lfsRepairs;
    private readonly IStorageNodeLivenessProbe _livenessProbe;
    private readonly ILogger<PromotePrimaryReplicaQueryHandler> _logger;

    public PromotePrimaryReplicaQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IColdRecoveryService coldRecoveryService,
        ILfsRepairQueue lfsRepairs,
        IStorageNodeLivenessProbe livenessProbe,
        ILogger<PromotePrimaryReplicaQueryHandler> logger
    )
    {
        _contextFactory = contextFactory;
        _coldRecoveryService = coldRecoveryService;
        _lfsRepairs = lfsRepairs;
        _livenessProbe = livenessProbe;
        _logger = logger;
    }

    public async Task<Option<PromotePrimaryReplicaResult>> RunQueryAsync(
        PromotePrimaryReplicaQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var entity = await context
            .Set<RepositoryEntity>()
            .Include(repository => repository.Replicas)
            .FirstOrDefaultAsync(
                repository => repository.Id == query.RepositoryId.Value,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (entity is null || entity.PrimaryStorageNodeId is null || entity.Replicas.Count == 0)
        {
            return Option.From(new PromotePrimaryReplicaResult { Promoted = false });
        }

        var nodeIds = entity.Replicas.Select(replica => replica.StorageNodeId).ToList();
        var nodes = await context
            .Set<StorageNodeEntity>()
            .AsNoTracking()
            .Where(node => nodeIds.Contains(node.Id))
            .ToDictionaryAsync(node => node.Id, cancellationToken)
            .ConfigureAwait(false);

        var primaryHealthy = nodes.TryGetValue(entity.PrimaryStorageNodeId.Value, out var primaryNode)
            && primaryNode.IsHealthy;

        if (primaryHealthy)
        {
            return Option.From(new PromotePrimaryReplicaResult { Promoted = false });
        }

        // Unhealthy only means the heartbeats stopped arriving, and they also stop when the API or the database
        // cannot take them: on 2026-10-18 a database stall left every node without a recent heartbeat, and failover
        // moved a working primary onto its encrypted replica. A primary that still answers stays the primary.
        if (primaryNode is not null
            && await _livenessProbe
                .AnswersAsync(primaryNode.InternalHost, primaryNode.InternalHttpPort, cancellationToken)
                .ConfigureAwait(false))
        {
            _logger.LogWarning(
                "Storage node {NodeId} sends no heartbeats but answers; repository {RepositoryId} stays on it.",
                primaryNode.NodeId,
                entity.Id
            );
            return Option.From(new PromotePrimaryReplicaResult { Promoted = false });
        }

        // A Degraded repository with encrypted replicas is an RF4 repository (ReplicationStateEvaluator applies the
        // same test) and fails over the RF4 way. RF4 commits a push once the primary and one encrypted replica hold
        // it, so no plaintext replica is sure to have every committed push. The RF3 rule, highest watermark wins, is
        // only safe behind RF3's two-of-three plaintext quorum: here it would roll committed pushes back. The RF4 path
        // promotes only a plaintext copy at PrimaryWatermark, otherwise restores from the encrypted copies or stays
        // Degraded, and keeps the repository on RF4: Rf3Healthy would send it through the RF4 backfill again, which
        // plans every role anew.
        if (entity.ReplicationState is ReplicationState.Rf4Healthy or ReplicationState.Recovering
            || (entity.ReplicationState == ReplicationState.Degraded
                && entity.Replicas.Any(replica => replica.Role == RepositoryReplicaRole.EncryptedReplica)))
        {
            return await PromoteRf4Async(context, entity, nodes, cancellationToken)
                .ConfigureAwait(false);
        }

        return await PromoteRf3Async(context, entity, nodes, cancellationToken).ConfigureAwait(false);
    }

    private async Task<Option<PromotePrimaryReplicaResult>> PromoteRf3Async(
        OpenGitBaseDbContext context,
        RepositoryEntity entity,
        IReadOnlyDictionary<Guid, StorageNodeEntity> nodes,
        CancellationToken cancellationToken
    )
    {
        // An encrypted replica holds no plaintext repository, and a node that is down cannot serve one: either as
        // primary denies every clone and push. Watermark commits advance encrypted rows too, so without this filter
        // they often win on watermark.
        var candidate = entity
            .Replicas.Where(replica =>
                replica.StorageNodeId != entity.PrimaryStorageNodeId
                && replica.Role != RepositoryReplicaRole.EncryptedReplica
                && nodes.TryGetValue(replica.StorageNodeId, out var node)
                && node.IsHealthy
            )
            .OrderByDescending(replica => replica.AppliedWatermark)
            .ThenBy(replica => replica.StorageNodeId)
            .FirstOrDefault();

        if (candidate is null)
        {
            entity.ReplicationState = ReplicationState.Degraded;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Option.From(new PromotePrimaryReplicaResult { Promoted = false });
        }

        entity.ReplicationState = ReplicationState.Promoting;
        entity.ReplicationEpoch += 1;
        entity.PrimaryStorageNodeId = candidate.StorageNodeId;
        entity.StorageNodeId = candidate.StorageNodeId;

        // Only the candidate and the old primary change roles. An encrypted replica holds artifacts, not a
        // repository; made a plaintext replica, it would be routed clones it cannot serve.
        foreach (var replica in entity.Replicas)
        {
            if (replica.StorageNodeId == candidate.StorageNodeId)
            {
                replica.Role = RepositoryReplicaRole.Primary;
            }
            else if (replica.Role == RepositoryReplicaRole.Primary)
            {
                replica.Role = RepositoryReplicaRole.Replica;
            }
        }

        entity.ReplicationState = ReplicationState.Rf3Healthy;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _lfsRepairs.Schedule(entity.Id);

        return Option.From(
            new PromotePrimaryReplicaResult
            {
                Promoted = true,
                NewPrimaryStorageNodeId = candidate.StorageNodeId,
                ReplicationEpoch = entity.ReplicationEpoch,
            }
        );
    }

    private async Task<Option<PromotePrimaryReplicaResult>> PromoteRf4Async(
        OpenGitBaseDbContext context,
        RepositoryEntity entity,
        IReadOnlyDictionary<Guid, StorageNodeEntity> nodes,
        CancellationToken cancellationToken
    )
    {
        // The read replica first, then any other plaintext copy (an RF3 replica the migration left, a demoted
        // primary), but only one at PrimaryWatermark: RF4 commits a push without the plaintext replicas.
        var candidate = entity
            .Replicas.Where(replica =>
                replica.Role != RepositoryReplicaRole.EncryptedReplica
                && replica.StorageNodeId != entity.PrimaryStorageNodeId
                && nodes.TryGetValue(replica.StorageNodeId, out var node)
                && node.IsHealthy
                && ReplicationSync.IsInSync(replica.AppliedWatermark, entity.PrimaryWatermark)
            )
            .OrderByDescending(replica => replica.Role == RepositoryReplicaRole.ReadReplica)
            .ThenBy(replica => replica.StorageNodeId)
            .FirstOrDefault();

        if (candidate is not null)
        {
            entity.ReplicationState = ReplicationState.Promoting;
            entity.ReplicationEpoch += 1;
            entity.PrimaryStorageNodeId = candidate.StorageNodeId;
            entity.StorageNodeId = candidate.StorageNodeId;

            foreach (var replica in entity.Replicas)
            {
                if (replica.StorageNodeId == candidate.StorageNodeId)
                {
                    replica.Role = RepositoryReplicaRole.Primary;
                }
                else if (replica.Role == RepositoryReplicaRole.Primary)
                {
                    replica.Role = RepositoryReplicaRole.Replica;
                }
            }

            entity.ReplicationState = ReplicationState.Rf4Healthy;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // LFS objects the promoted node never received are restored first, ahead of the next reconciler pass.
            _lfsRepairs.Schedule(entity.Id);

            return Option.From(
                new PromotePrimaryReplicaResult
                {
                    Promoted = true,
                    NewPrimaryStorageNodeId = candidate.StorageNodeId,
                    ReplicationEpoch = entity.ReplicationEpoch,
                }
            );
        }

        var plaintextHealthy = entity.Replicas.Any(replica =>
            replica.Role is RepositoryReplicaRole.Primary or RepositoryReplicaRole.ReadReplica
            && nodes.TryGetValue(replica.StorageNodeId, out var node)
            && node.IsHealthy
        );

        if (!plaintextHealthy)
        {
            var recovered = await _coldRecoveryService
                .TryRecoverAsync(entity.Id, cancellationToken)
                .ConfigureAwait(false);
            if (recovered)
            {
                return Option.From(
                    new PromotePrimaryReplicaResult
                    {
                        Promoted = true,
                        NewPrimaryStorageNodeId = entity.PrimaryStorageNodeId,
                        ReplicationEpoch = entity.ReplicationEpoch,
                    }
                );
            }
        }

        entity.ReplicationState = ReplicationState.Degraded;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Option.From(new PromotePrimaryReplicaResult { Promoted = false });
    }
}
