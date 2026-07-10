using Microsoft.EntityFrameworkCore;
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

    public PromotePrimaryReplicaQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IColdRecoveryService coldRecoveryService
    )
    {
        _contextFactory = contextFactory;
        _coldRecoveryService = coldRecoveryService;
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

        if (entity.ReplicationState is ReplicationState.Rf4Healthy or ReplicationState.Recovering)
        {
            return await PromoteRf4Async(context, entity, nodes, cancellationToken)
                .ConfigureAwait(false);
        }

        return await PromoteRf3Async(context, entity, cancellationToken).ConfigureAwait(false);
    }

    private static async Task<Option<PromotePrimaryReplicaResult>> PromoteRf3Async(
        OpenGitBaseDbContext context,
        RepositoryEntity entity,
        CancellationToken cancellationToken
    )
    {
        var candidate = entity
            .Replicas.Where(replica => replica.StorageNodeId != entity.PrimaryStorageNodeId)
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

        foreach (var replica in entity.Replicas)
        {
            replica.Role = replica.StorageNodeId == candidate.StorageNodeId
                ? RepositoryReplicaRole.Primary
                : RepositoryReplicaRole.Replica;
        }

        entity.ReplicationState = ReplicationState.Rf3Healthy;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

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
        var readReplica = entity.Replicas.FirstOrDefault(replica =>
            replica.Role == RepositoryReplicaRole.ReadReplica
            && nodes.TryGetValue(replica.StorageNodeId, out var node)
            && node.IsHealthy
            && replica.AppliedWatermark >= entity.PrimaryWatermark
        );

        if (readReplica is not null)
        {
            entity.ReplicationState = ReplicationState.Promoting;
            entity.ReplicationEpoch += 1;
            entity.PrimaryStorageNodeId = readReplica.StorageNodeId;
            entity.StorageNodeId = readReplica.StorageNodeId;

            foreach (var replica in entity.Replicas)
            {
                if (replica.StorageNodeId == readReplica.StorageNodeId)
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

            return Option.From(
                new PromotePrimaryReplicaResult
                {
                    Promoted = true,
                    NewPrimaryStorageNodeId = readReplica.StorageNodeId,
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
