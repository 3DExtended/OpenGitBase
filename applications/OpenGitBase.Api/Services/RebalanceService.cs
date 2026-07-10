using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Api.Services;

public sealed class RebalanceService
{
    private const int MtlsGitHttpPort = 8443;

    private static readonly ConcurrentDictionary<(Guid RepositoryId, Guid ReplacementNodeId), Guid>
        PendingReplacements = new();

    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IQueryProcessor _queryProcessor;
    private readonly IStorageProvisionerClient _storageProvisionerClient;

    public RebalanceService(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IQueryProcessor queryProcessor,
        IStorageProvisionerClient storageProvisionerClient
    )
    {
        _contextFactory = contextFactory;
        _queryProcessor = queryProcessor;
        _storageProvisionerClient = storageProvisionerClient;
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await ReattachRecoveredNodesAsync(context, cancellationToken).ConfigureAwait(false);

        var unhealthyNodeIds = await context
            .Set<StorageNodeEntity>()
            .AsNoTracking()
            .Where(node => !node.IsHealthy)
            .Select(node => node.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (unhealthyNodeIds.Count == 0)
        {
            return;
        }

        var affected = await context
            .Set<RepositoryReplicaEntity>()
            .Where(replica => unhealthyNodeIds.Contains(replica.StorageNodeId))
            .Select(replica => replica.RepositoryId)
            .Distinct()
            .Take(10)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var repositoryId in affected)
        {
            await RebalanceRepositoryAsync(context, repositoryId, unhealthyNodeIds, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    internal static void TrackPendingReplacement(
        Guid repositoryId,
        Guid replacementNodeId,
        Guid replacedNodeId
    ) => PendingReplacements[(repositoryId, replacementNodeId)] = replacedNodeId;

    internal static void ClearPendingReplacement(Guid repositoryId, Guid replacementNodeId) =>
        PendingReplacements.TryRemove((repositoryId, replacementNodeId), out _);

    private static ReplicationState EvaluateReplicationState(RepositoryEntity repository)
    {
        if (repository.ReplicationState is ReplicationState.Rf4Healthy or ReplicationState.Rf4Migrating or ReplicationState.Recovering)
        {
            var encryptedCount = repository.Replicas.Count(replica =>
                replica.Role == RepositoryReplicaRole.EncryptedReplica
                && replica.ArtifactWatermark >= repository.PrimaryWatermark
            );
            var plaintextInSync = repository.Replicas.Count(replica =>
                replica.Role != RepositoryReplicaRole.EncryptedReplica
                && ReplicationSync.IsInSync(replica.AppliedWatermark, repository.PrimaryWatermark)
            );
            return encryptedCount >= 1 && plaintextInSync >= 2
                ? ReplicationState.Rf4Healthy
                : ReplicationState.Degraded;
        }

        var inSyncCount = repository.Replicas.Count(replica =>
            ReplicationSync.IsInSync(replica.AppliedWatermark, repository.PrimaryWatermark)
        );
        return inSyncCount >= 2 ? ReplicationState.Rf3Healthy : ReplicationState.Degraded;
    }

    private async Task ReattachRecoveredNodesAsync(
        OpenGitBaseDbContext context,
        CancellationToken cancellationToken
    )
    {
        if (PendingReplacements.IsEmpty)
        {
            return;
        }

        var healthyNodeIds = await context
            .Set<StorageNodeEntity>()
            .AsNoTracking()
            .Where(node => node.IsHealthy)
            .Select(node => node.Id)
            .ToHashSetAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var pending in PendingReplacements.ToArray())
        {
            var (repositoryId, replacementNodeId) = pending.Key;
            var originalNodeId = pending.Value;
            if (!healthyNodeIds.Contains(originalNodeId))
            {
                continue;
            }

            var repository = await context
                .Set<RepositoryEntity>()
                .Include(entity => entity.Replicas)
                .FirstOrDefaultAsync(entity => entity.Id == repositoryId, cancellationToken)
                .ConfigureAwait(false);
            if (repository is null)
            {
                ClearPendingReplacement(repositoryId, replacementNodeId);
                continue;
            }

            var replacementReplica = repository.Replicas.FirstOrDefault(replica =>
                replica.StorageNodeId == replacementNodeId
            );
            if (replacementReplica is null)
            {
                ClearPendingReplacement(repositoryId, replacementNodeId);
                continue;
            }

            if (
                ReplicationSync.IsInSync(
                    replacementReplica.AppliedWatermark,
                    repository.PrimaryWatermark
                )
            )
            {
                ClearPendingReplacement(repositoryId, replacementNodeId);
                continue;
            }

            repository.Replicas.Remove(replacementReplica);
            repository.Replicas.Add(
                new RepositoryReplicaEntity
                {
                    RepositoryId = repository.Id,
                    StorageNodeId = originalNodeId,
                    Role = RepositoryReplicaRole.Replica,
                    AppliedWatermark = replacementReplica.AppliedWatermark,
                    LastSyncedAt = replacementReplica.LastSyncedAt,
                }
            );

            repository.ReplicationState = EvaluateReplicationState(repository);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            ClearPendingReplacement(repositoryId, replacementNodeId);
        }
    }

    private async Task RebalanceRepositoryAsync(
        OpenGitBaseDbContext context,
        Guid repositoryId,
        IReadOnlyList<Guid> unhealthyNodeIds,
        CancellationToken cancellationToken
    )
    {
        var repository = await context
            .Set<RepositoryEntity>()
            .Include(entity => entity.Replicas)
            .FirstOrDefaultAsync(entity => entity.Id == repositoryId, cancellationToken)
            .ConfigureAwait(false);

        if (repository is null || repository.PrimaryStorageNodeId is null)
        {
            return;
        }

        var deadReplica = repository.Replicas.FirstOrDefault(replica =>
            unhealthyNodeIds.Contains(replica.StorageNodeId)
        );
        if (deadReplica is null)
        {
            return;
        }

        if (
            repository.Replicas.Any(replica =>
                PendingReplacements.ContainsKey((repositoryId, replica.StorageNodeId))
            )
        )
        {
            return;
        }

        var healthyNodes = await _queryProcessor
            .RunQueryAsync(new ListHealthyStorageNodesQuery(), cancellationToken)
            .ConfigureAwait(false);
        var nodes = healthyNodes.IsSome ? healthyNodes.Get() : Array.Empty<StorageNodeDto>();
        var memberIds = repository.Replicas.Select(replica => replica.StorageNodeId).ToHashSet();
        var replacement = nodes
            .Where(node => !memberIds.Contains(node.Id.Value))
            .OrderByDescending(node => node.FreeBytesAvailable)
            .FirstOrDefault();

        if (replacement is null)
        {
            repository.ReplicationState = ReplicationState.Degraded;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return;
        }

        var primaryNode = nodes.FirstOrDefault(node =>
            node.Id.Value == repository.PrimaryStorageNodeId
        );
        if (primaryNode is null)
        {
            return;
        }

        var token = await GetApiTokenAsync(replacement.Id, cancellationToken).ConfigureAwait(false);
        if (token is null)
        {
            return;
        }

        var provisionRole = deadReplica.Role switch
        {
            RepositoryReplicaRole.ReadReplica => nameof(RepositoryReplicaRole.ReadReplica),
            RepositoryReplicaRole.EncryptedReplica => nameof(RepositoryReplicaRole.EncryptedReplica),
            _ => nameof(RepositoryReplicaRole.Replica),
        };

        var provision = await _storageProvisionerClient
            .ProvisionRepositoryAsync(
                replacement,
                token,
                repository.PhysicalPath,
                receiveMaxBytes: 0,
                replicationRole: provisionRole,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (!provision.Success)
        {
            return;
        }

        if (deadReplica.Role == RepositoryReplicaRole.EncryptedReplica)
        {
            var sourceEncrypted = repository.Replicas.FirstOrDefault(replica =>
                replica.Role == RepositoryReplicaRole.EncryptedReplica
                && replica.StorageNodeId != deadReplica.StorageNodeId
                && replica.ArtifactWatermark >= repository.PrimaryWatermark
            );
            if (sourceEncrypted is null)
            {
                return;
            }

            var sourceNode = nodes.FirstOrDefault(node =>
                node.Id.Value == sourceEncrypted.StorageNodeId
            );
            var sourceToken = sourceNode is null
                ? null
                : await GetApiTokenAsync(sourceNode.Id, cancellationToken).ConfigureAwait(false);
            if (sourceNode is null || sourceToken is null)
            {
                return;
            }

            var artifact = await _storageProvisionerClient
                .TryGetReplicationArtifactAsync(
                    sourceNode,
                    sourceToken,
                    repository.Id,
                    repository.PrimaryWatermark,
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (!artifact.Success)
            {
                return;
            }

            var upload = await _storageProvisionerClient
                .UploadReplicationArtifactAsync(
                    replacement,
                    token,
                    repository.Id,
                    repository.PrimaryWatermark,
                    artifact.ManifestJson,
                    artifact.BundlePayload,
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (!upload.Success)
            {
                return;
            }
        }
        else
        {
            await _storageProvisionerClient
                .SyncRepositoryFromPeerAsync(
                    replacement,
                    token,
                    repository.PhysicalPath,
                    primaryNode.InternalHost,
                    repository.PhysicalPath,
                    MtlsGitHttpPort,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        var replacedNodeId = deadReplica.StorageNodeId;
        context.Remove(deadReplica);
        repository.Replicas.Add(
            new RepositoryReplicaEntity
            {
                RepositoryId = repository.Id,
                StorageNodeId = replacement.Id.Value,
                Role = deadReplica.Role == RepositoryReplicaRole.Primary
                    ? RepositoryReplicaRole.Replica
                    : deadReplica.Role,
                AppliedWatermark = repository.PrimaryWatermark,
                ArtifactWatermark = deadReplica.Role == RepositoryReplicaRole.EncryptedReplica
                    ? repository.PrimaryWatermark
                    : null,
                LastSyncedAt = DateTimeOffset.UtcNow,
            }
        );
        TrackPendingReplacement(repository.Id, replacement.Id.Value, replacedNodeId);

        repository.ReplicationState = EvaluateReplicationState(repository);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<string?> GetApiTokenAsync(
        StorageNodeId storageNodeId,
        CancellationToken cancellationToken
    )
    {
        var result = await _queryProcessor
            .RunQueryAsync(
                new GetStorageNodeApiTokenQuery { StorageNodeId = storageNodeId },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsSome ? result.Get() : null;
    }
}
