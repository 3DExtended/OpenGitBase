using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Services;

/// <summary>
/// Brings the replicas a committed write's quorum did not include up to that write: plaintext
/// replicas by a fetch from the primary, encrypted replicas by uploading the write's artifact.
/// <para>
/// Runs after the caller has had its answer, so it never uses the caller's services: the request
/// (or reconciler) scope that committed the write is disposed by then, and anything resolved from
/// it throws. <see cref="StartInBackground"/> gives every run a scope of its own. Whatever is left
/// behind stays lagging on purpose: the anti-entropy reconciler repairs replicas below
/// PrimaryWatermark.
/// </para>
/// </summary>
public sealed class ReplicaCatchUpService
{
    private const int MtlsGitHttpPort = 8443;

    private readonly IQueryProcessor _queryProcessor;
    private readonly IStorageProvisionerClient _storageProvisionerClient;
    private readonly ReplicationArtifactPublisher _artifactPublisher;
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly ILogger<ReplicaCatchUpService> _logger;

    public ReplicaCatchUpService(
        IQueryProcessor queryProcessor,
        IStorageProvisionerClient storageProvisionerClient,
        ReplicationArtifactPublisher artifactPublisher,
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        ILogger<ReplicaCatchUpService> logger
    )
    {
        _queryProcessor = queryProcessor;
        _storageProvisionerClient = storageProvisionerClient;
        _artifactPublisher = artifactPublisher;
        _contextFactory = contextFactory;
        _logger = logger;
    }

    /// <summary>
    /// Runs <paramref name="work"/> on a thread-pool thread in a DI scope of its own and logs what
    /// it throws instead of leaving the exception unobserved.
    /// </summary>
    public static void StartInBackground(
        IServiceScopeFactory scopeFactory,
        Func<ReplicaCatchUpService, Task> work
    )
    {
        _ = Task.Run(
            async () =>
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var catchUp = scope.ServiceProvider.GetRequiredService<ReplicaCatchUpService>();
                try
                {
                    await work(catchUp).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    catchUp._logger.LogWarning(
                        ex,
                        "Background replica catch-up failed; the reconciler repairs replicas that are still behind."
                    );
                }
            },
            CancellationToken.None
        );
    }

    /// <summary>
    /// RF4: syncs the read replica from the primary and stores the artifact of
    /// <paramref name="watermark"/> on each of <paramref name="remainingEncryptedPeers"/>, using
    /// <paramref name="artifactInHand"/> or else downloading it from
    /// <paramref name="sourceEncryptedNodeId"/>. An encrypted replica is recorded only once its
    /// upload succeeded.
    /// </summary>
    public async Task CatchUpRf4Async(
        Guid repositoryId,
        string physicalPath,
        string primaryHost,
        RepositoryReplicationPeerDto? readReplicaPeer,
        IReadOnlyList<RepositoryReplicationPeerDto> remainingEncryptedPeers,
        Guid sourceEncryptedNodeId,
        long watermark,
        ReplicationArtifactFetchResult? artifactInHand,
        CancellationToken cancellationToken
    )
    {
        if (readReplicaPeer is not null)
        {
            await CatchUpReplicaAsync(
                    StorageNodeId.From(readReplicaPeer.StorageNodeId),
                    physicalPath,
                    primaryHost,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        if (remainingEncryptedPeers.Count == 0)
        {
            return;
        }

        var artifact = artifactInHand
            ?? await FetchArtifactFromPeerAsync(repositoryId, sourceEncryptedNodeId, watermark, cancellationToken)
                .ConfigureAwait(false);
        if (artifact is null || !artifact.Success)
        {
            return;
        }

        foreach (var encryptedPeer in remainingEncryptedPeers)
        {
            var targetNode = await LoadStorageNodeAsync(
                    StorageNodeId.From(encryptedPeer.StorageNodeId),
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (targetNode is null)
            {
                continue;
            }

            var upload = await _artifactPublisher
                .UploadAsync(targetNode, repositoryId, watermark, artifact, cancellationToken)
                .ConfigureAwait(false);
            if (!upload.Success)
            {
                continue;
            }

            await PersistEncryptedArtifactWatermarkAsync(
                    repositoryId,
                    encryptedPeer.StorageNodeId,
                    watermark,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
    }

    /// <summary>Has one plaintext replica fetch the repository from the primary.</summary>
    public async Task CatchUpReplicaAsync(
        StorageNodeId replicaNodeId,
        string physicalPath,
        string primaryHost,
        CancellationToken cancellationToken
    )
    {
        var replicaNode = await LoadStorageNodeAsync(replicaNodeId, cancellationToken)
            .ConfigureAwait(false);
        if (replicaNode is null)
        {
            return;
        }

        var token = await GetApiTokenAsync(replicaNode.Id, cancellationToken).ConfigureAwait(false);
        if (token is null)
        {
            return;
        }

        await _storageProvisionerClient
            .SyncRepositoryFromPeerAsync(
                replicaNode,
                token,
                physicalPath,
                primaryHost,
                physicalPath,
                MtlsGitHttpPort,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private async Task<ReplicationArtifactFetchResult?> FetchArtifactFromPeerAsync(
        Guid repositoryId,
        Guid sourceEncryptedNodeId,
        long watermark,
        CancellationToken cancellationToken
    )
    {
        var sourceNode = await LoadStorageNodeAsync(
                StorageNodeId.From(sourceEncryptedNodeId),
                cancellationToken
            )
            .ConfigureAwait(false);
        var sourceToken = sourceNode is null
            ? null
            : await GetApiTokenAsync(sourceNode.Id, cancellationToken).ConfigureAwait(false);
        if (sourceNode is null || sourceToken is null)
        {
            return null;
        }

        return await _storageProvisionerClient
            .TryGetReplicationArtifactAsync(
                sourceNode,
                sourceToken,
                repositoryId,
                watermark,
                cancellationToken
            )
            .ConfigureAwait(false);
    }

    private async Task PersistEncryptedArtifactWatermarkAsync(
        Guid repositoryId,
        Guid storageNodeId,
        long watermark,
        CancellationToken cancellationToken
    )
    {
        await using var db = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        var replica = await db
            .Set<RepositoryReplicaEntity>()
            .FirstOrDefaultAsync(
                entity =>
                    entity.RepositoryId == repositoryId && entity.StorageNodeId == storageNodeId,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (replica is null)
        {
            return;
        }

        replica.ArtifactWatermark = Math.Max(replica.ArtifactWatermark ?? 0, watermark);
        replica.LastSyncedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<StorageNodeDto?> LoadStorageNodeAsync(
        StorageNodeId storageNodeId,
        CancellationToken cancellationToken
    )
    {
        var result = await _queryProcessor
            .RunQueryAsync(new GetStorageNodeQuery { ModelId = storageNodeId }, cancellationToken)
            .ConfigureAwait(false);

        return result.IsSome ? result.Get() : null;
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
