using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Services;

public sealed class AntiEntropyReconcilerService
{
    private const int MtlsGitHttpPort = 8443;

    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IQueryProcessor _queryProcessor;
    private readonly IStorageProvisionerClient _storageProvisionerClient;
    private readonly Rf1BackfillService _backfillService;

    public AntiEntropyReconcilerService(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IQueryProcessor queryProcessor,
        IStorageProvisionerClient storageProvisionerClient,
        Rf1BackfillService backfillService
    )
    {
        _contextFactory = contextFactory;
        _queryProcessor = queryProcessor;
        _storageProvisionerClient = storageProvisionerClient;
        _backfillService = backfillService;
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var lagging = await context
            .Set<RepositoryEntity>()
            .Include(repository => repository.Replicas)
            .Where(repository =>
                repository.Replicas.Any(replica =>
                    replica.AppliedWatermark < repository.PrimaryWatermark
                )
            )
            .Take(10)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var repository in lagging)
        {
            await SyncLaggingReplicasAsync(context, repository, cancellationToken)
                .ConfigureAwait(false);
        }

        var degraded = await context
            .Set<RepositoryEntity>()
            .CountAsync(
                repository =>
                    repository.ReplicationState == ReplicationState.Degraded
                    || repository.ReplicationState == ReplicationState.Rf1Backfilling,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (degraded > 0)
        {
            await _backfillService.RunOnceAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task SyncLaggingReplicasAsync(
        OpenGitBaseDbContext context,
        RepositoryEntity repository,
        CancellationToken cancellationToken
    )
    {
        var primaryReplica = repository.Replicas.FirstOrDefault(replica =>
            replica.StorageNodeId == repository.PrimaryStorageNodeId
        );
        if (primaryReplica is null)
        {
            return;
        }

        var nodes = await _queryProcessor
            .RunQueryAsync(new ListHealthyStorageNodesQuery(), cancellationToken)
            .ConfigureAwait(false);
        if (nodes.IsNone)
        {
            return;
        }

        var nodeById = nodes.Get().ToDictionary(node => node.Id.Value);
        if (!nodeById.TryGetValue(primaryReplica.StorageNodeId, out var primaryNode))
        {
            return;
        }

        foreach (var replica in repository.Replicas)
        {
            if (replica.Role == RepositoryReplicaRole.EncryptedReplica)
            {
                if (
                    replica.ArtifactWatermark is null
                    || replica.ArtifactWatermark < repository.PrimaryWatermark
                )
                {
                    await RepairEncryptedArtifactAsync(
                            repository,
                            replica,
                            nodeById,
                            cancellationToken
                        )
                        .ConfigureAwait(false);
                }

                continue;
            }

            if (!ReplicationSync.IsInSync(replica.AppliedWatermark, repository.PrimaryWatermark))
            {
                if (!nodeById.TryGetValue(replica.StorageNodeId, out var replicaNode))
                {
                    continue;
                }

                var token = await GetApiTokenAsync(replicaNode.Id, cancellationToken)
                    .ConfigureAwait(false);
                if (token is null)
                {
                    continue;
                }

                var sync = await _storageProvisionerClient
                    .SyncRepositoryFromPeerAsync(
                        replicaNode,
                        token,
                        repository.PhysicalPath,
                        primaryNode.InternalHost,
                        repository.PhysicalPath,
                        MtlsGitHttpPort,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                if (sync.Success)
                {
                    replica.AppliedWatermark = repository.PrimaryWatermark;
                    replica.LastSyncedAt = DateTimeOffset.UtcNow;
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RepairEncryptedArtifactAsync(
        RepositoryEntity repository,
        RepositoryReplicaEntity replica,
        IReadOnlyDictionary<Guid, StorageNodeDto> nodeById,
        CancellationToken cancellationToken
    )
    {
        var sourceReplica = repository.Replicas
            .Where(candidate =>
                candidate.Role == RepositoryReplicaRole.EncryptedReplica
                && candidate.StorageNodeId != replica.StorageNodeId
                && candidate.ArtifactWatermark >= repository.PrimaryWatermark
            )
            .FirstOrDefault();
        if (sourceReplica is null)
        {
            return;
        }

        if (
            !nodeById.TryGetValue(sourceReplica.StorageNodeId, out var sourceNode)
            || !nodeById.TryGetValue(replica.StorageNodeId, out var targetNode)
        )
        {
            return;
        }

        var sourceToken = await GetApiTokenAsync(sourceNode.Id, cancellationToken)
            .ConfigureAwait(false);
        var targetToken = await GetApiTokenAsync(targetNode.Id, cancellationToken)
            .ConfigureAwait(false);
        if (sourceToken is null || targetToken is null)
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
                targetNode,
                targetToken,
                repository.Id,
                repository.PrimaryWatermark,
                artifact.ManifestJson,
                artifact.BundlePayload,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (upload.Success)
        {
            replica.ArtifactWatermark = repository.PrimaryWatermark;
            replica.LastSyncedAt = DateTimeOffset.UtcNow;
        }
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
