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
    private readonly IRepositoryKeyService _repositoryKeyService;
    private readonly Rf1BackfillService _backfillService;

    public AntiEntropyReconcilerService(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IQueryProcessor queryProcessor,
        IStorageProvisionerClient storageProvisionerClient,
        IRepositoryKeyService repositoryKeyService,
        Rf1BackfillService backfillService
    )
    {
        _contextFactory = contextFactory;
        _queryProcessor = queryProcessor;
        _storageProvisionerClient = storageProvisionerClient;
        _repositoryKeyService = repositoryKeyService;
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
                    || (
                        replica.Role == RepositoryReplicaRole.EncryptedReplica
                        && (
                            replica.ArtifactWatermark == null
                            || replica.ArtifactWatermark < repository.PrimaryWatermark
                        )
                    )
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

        ReplicationArtifactFetchResult? bootstrappedArtifact = null;

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
                            primaryNode,
                            nodeById,
                            () => bootstrappedArtifact,
                            artifact => bootstrappedArtifact = artifact,
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
        StorageNodeDto primaryNode,
        IReadOnlyDictionary<Guid, StorageNodeDto> nodeById,
        Func<ReplicationArtifactFetchResult?> getBootstrappedArtifact,
        Action<ReplicationArtifactFetchResult> setBootstrappedArtifact,
        CancellationToken cancellationToken
    )
    {
        if (!nodeById.TryGetValue(replica.StorageNodeId, out var targetNode))
        {
            return;
        }

        var targetToken = await GetApiTokenAsync(targetNode.Id, cancellationToken)
            .ConfigureAwait(false);
        if (targetToken is null)
        {
            return;
        }

        if (repository.PrimaryWatermark <= 0)
        {
            replica.ArtifactWatermark = repository.PrimaryWatermark;
            replica.LastSyncedAt = DateTimeOffset.UtcNow;
            return;
        }

        var artifact = await ResolveEncryptedArtifactAsync(
                repository,
                primaryNode,
                nodeById,
                getBootstrappedArtifact,
                setBootstrappedArtifact,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (artifact is null || !artifact.Success)
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

    private async Task<ReplicationArtifactFetchResult?> ResolveEncryptedArtifactAsync(
        RepositoryEntity repository,
        StorageNodeDto primaryNode,
        IReadOnlyDictionary<Guid, StorageNodeDto> nodeById,
        Func<ReplicationArtifactFetchResult?> getBootstrappedArtifact,
        Action<ReplicationArtifactFetchResult> setBootstrappedArtifact,
        CancellationToken cancellationToken
    )
    {
        var cached = getBootstrappedArtifact();
        if (cached is not null)
        {
            return cached;
        }

        var sourceReplica = repository.Replicas
            .Where(candidate =>
                candidate.Role == RepositoryReplicaRole.EncryptedReplica
                && candidate.ArtifactWatermark >= repository.PrimaryWatermark
            )
            .FirstOrDefault();
        if (sourceReplica is not null
            && nodeById.TryGetValue(sourceReplica.StorageNodeId, out var sourceNode))
        {
            var sourceToken = await GetApiTokenAsync(sourceNode.Id, cancellationToken)
                .ConfigureAwait(false);
            if (sourceToken is not null)
            {
                var fromPeer = await _storageProvisionerClient
                    .TryGetReplicationArtifactAsync(
                        sourceNode,
                        sourceToken,
                        repository.Id,
                        repository.PrimaryWatermark,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                if (fromPeer.Success)
                {
                    setBootstrappedArtifact(fromPeer);
                    return fromPeer;
                }
            }
        }

        var primaryToken = await GetApiTokenAsync(primaryNode.Id, cancellationToken)
            .ConfigureAwait(false);
        if (primaryToken is null)
        {
            return null;
        }

        var key = await _repositoryKeyService
            .TryGetRepositoryKeyAsync(repository.Id, cancellationToken)
            .ConfigureAwait(false);
        if (key is null)
        {
            return null;
        }

        var created = await _storageProvisionerClient
            .CreateReplicationArtifactAsync(
                primaryNode,
                primaryToken,
                repository.PhysicalPath,
                repository.Id,
                repository.PrimaryWatermark,
                repository.ReplicationEpoch,
                Convert.ToHexString(key.KeyMaterial).ToLowerInvariant(),
                key.KeyVersion,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (created.Success)
        {
            setBootstrappedArtifact(created);
        }

        return created;
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
