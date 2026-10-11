using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Services;

public sealed class QuorumReplicateRepositoryQueryHandler
    : IQueryHandler<QuorumReplicateRepositoryQuery, QuorumReplicateRepositoryResult>
{
    private const int MtlsGitHttpPort = 8443;

    private readonly IQueryProcessor _queryProcessor;
    private readonly IStorageProvisionerClient _storageProvisionerClient;
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly ReplicationArtifactPublisher _artifactPublisher;

    public QuorumReplicateRepositoryQueryHandler(
        IQueryProcessor queryProcessor,
        IStorageProvisionerClient storageProvisionerClient,
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        ReplicationArtifactPublisher artifactPublisher
    )
    {
        _queryProcessor = queryProcessor;
        _storageProvisionerClient = storageProvisionerClient;
        _contextFactory = contextFactory;
        _artifactPublisher = artifactPublisher;
    }

    public async Task<Option<QuorumReplicateRepositoryResult>> RunQueryAsync(
        QuorumReplicateRepositoryQuery query,
        CancellationToken cancellationToken
    )
    {
        var contextResult = await _queryProcessor
            .RunQueryAsync(
                new GetRepositoryReplicationContextQuery
                {
                    RepositoryId = query.RepositoryId,
                    StorageNodeId = query.StorageNodeId,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (contextResult.IsNone)
        {
            return Option.From(
                QuorumReplicateRepositoryResult.Failed("Repository replication context unavailable.")
            );
        }

        var context = contextResult.Get();
        if (!context.IsPrimary)
        {
            return Option.From(
                QuorumReplicateRepositoryResult.Failed(
                    "Quorum replication may only be initiated by the primary."
                )
            );
        }

        if (query.AppliedWatermark <= context.PrimaryWatermark)
        {
            return Option.From(
                QuorumReplicateRepositoryResult.StaleWatermark(
                    $"Watermark {query.AppliedWatermark} is already committed (primary watermark {context.PrimaryWatermark})."
                )
            );
        }

        if (query.AppliedWatermark != context.PrimaryWatermark + 1)
        {
            return Option.From(
                QuorumReplicateRepositoryResult.Failed(
                    "Applied watermark must be exactly one greater than the primary watermark."
                )
            );
        }

        if (string.Equals(context.ReplicationState, nameof(ReplicationState.Rf4Healthy), StringComparison.Ordinal))
        {
            return query.CreateArtifact
                ? await RunRf4CreateArtifactQuorumAsync(query, context, cancellationToken).ConfigureAwait(false)
                : await RunRf4QuorumAsync(query, context, cancellationToken).ConfigureAwait(false);
        }

        return await RunRf3QuorumAsync(query, context, cancellationToken).ConfigureAwait(false);
    }

    private static string Describe(int statusCode, string? error)
    {
        var detail = string.IsNullOrWhiteSpace(error) ? "no detail" : error.Trim();
        return statusCode > 0 ? $"HTTP {statusCode}: {detail}" : detail;
    }

    private async Task<Option<QuorumReplicateRepositoryResult>> RunRf4QuorumAsync(
        QuorumReplicateRepositoryQuery query,
        RepositoryReplicationContextDto context,
        CancellationToken cancellationToken
    )
    {
        var confirmedEncryptedNodeIds = query.ConfirmedEncryptedNodeIds
            .Where(id => id != Guid.Empty)
            .Distinct()
            .ToList();

        if (confirmedEncryptedNodeIds.Count == 0)
        {
            return Option.From(
                QuorumReplicateRepositoryResult.Failed(
                    "At least one encrypted replica must confirm artifact receipt."
                )
            );
        }

        var encryptedPeers = context.Peers
            .Where(peer =>
                peer.IsHealthy
                && string.Equals(
                    peer.Role,
                    nameof(RepositoryReplicaRole.EncryptedReplica),
                    StringComparison.Ordinal
                )
            )
            .ToList();

        if (encryptedPeers.Count == 0)
        {
            return Option.From(
                QuorumReplicateRepositoryResult.Failed(
                    "Write quorum unavailable: no healthy encrypted replicas."
                )
            );
        }

        var quorumNodeIds = new List<StorageNodeId> { query.StorageNodeId };
        foreach (var encryptedNodeId in confirmedEncryptedNodeIds)
        {
            var peer = encryptedPeers.FirstOrDefault(candidate =>
                candidate.StorageNodeId == encryptedNodeId
            );
            if (peer is null)
            {
                return Option.From(
                    QuorumReplicateRepositoryResult.Failed(
                        "Confirmed encrypted replica is not a healthy encrypted member."
                    )
                );
            }

            var node = await LoadStorageNodeAsync(
                    StorageNodeId.From(encryptedNodeId),
                    cancellationToken
                )
                .ConfigureAwait(false);
            var token = node is null
                ? null
                : await GetApiTokenAsync(node.Id, cancellationToken).ConfigureAwait(false);
            if (token is null)
            {
                return Option.From(
                    QuorumReplicateRepositoryResult.Failed(
                        "Confirmed encrypted replica credentials are unavailable."
                    )
                );
            }

            var artifact = await _storageProvisionerClient
                .TryGetReplicationArtifactAsync(
                    node,
                    token,
                    context.RepositoryId,
                    query.AppliedWatermark,
                    cancellationToken
                )
                .ConfigureAwait(false);

            if (!artifact.Success)
            {
                return Option.From(
                    QuorumReplicateRepositoryResult.Failed(
                        "Encrypted artifact confirmation could not be verified."
                    )
                );
            }

            quorumNodeIds.Add(StorageNodeId.From(encryptedNodeId));
        }

        var commitResult = await _queryProcessor
            .RunQueryAsync(
                new CommitReplicationWatermarkQuery
                {
                    RepositoryId = query.RepositoryId,
                    StorageNodeId = query.StorageNodeId,
                    ReplicationEpoch = context.ReplicationEpoch,
                    NewWatermark = query.AppliedWatermark,
                    QuorumNodeIds = quorumNodeIds,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (commitResult.IsNone || !commitResult.Get().Success)
        {
            var error = commitResult.IsSome
                ? commitResult.Get().Error
                : "Watermark commit failed.";
            return Option.From(QuorumReplicateRepositoryResult.Failed(error!));
        }

        var primaryPeer = context.Peers.First(peer =>
            string.Equals(peer.Role, nameof(RepositoryReplicaRole.Primary), StringComparison.Ordinal)
        );
        var readReplicaPeer = context.Peers.FirstOrDefault(peer =>
            peer.IsHealthy
            && string.Equals(
                peer.Role,
                nameof(RepositoryReplicaRole.ReadReplica),
                StringComparison.Ordinal
            )
        );
        var remainingEncryptedPeers = encryptedPeers
            .Where(peer => !confirmedEncryptedNodeIds.Contains(peer.StorageNodeId))
            .ToList();

        _ = Task.Run(
            () =>
                CatchUpRf4Async(
                    context.RepositoryId,
                    context.PhysicalPath,
                    primaryPeer.InternalHost,
                    readReplicaPeer,
                    remainingEncryptedPeers,
                    confirmedEncryptedNodeIds[0],
                    query.AppliedWatermark,
                    artifactInHand: null,
                    CancellationToken.None
                ),
            CancellationToken.None
        );

        return Option.From(
            QuorumReplicateRepositoryResult.Replicated(commitResult.Get().PrimaryWatermark)
        );
    }

    /// <summary>
    /// RF4 push path where the API builds the artifact itself (#413): create the encrypted bundle
    /// on the primary, upload it to every healthy encrypted replica, and commit the watermark with
    /// the primary plus every replica that stored it. Nothing is downloaded again to confirm it:
    /// the uploads' own 2xx answers come from this request. Replicas that refused are retried by
    /// the fan-out with the artifact still in hand.
    /// </summary>
    private async Task<Option<QuorumReplicateRepositoryResult>> RunRf4CreateArtifactQuorumAsync(
        QuorumReplicateRepositoryQuery query,
        RepositoryReplicationContextDto context,
        CancellationToken cancellationToken
    )
    {
        var encryptedPeers = context.Peers
            .Where(peer =>
                peer.IsHealthy
                && string.Equals(
                    peer.Role,
                    nameof(RepositoryReplicaRole.EncryptedReplica),
                    StringComparison.Ordinal
                )
            )
            .ToList();
        if (encryptedPeers.Count == 0)
        {
            return Option.From(
                QuorumReplicateRepositoryResult.Failed(
                    "Write quorum unavailable: no healthy encrypted replicas."
                )
            );
        }

        var primaryNode = await LoadStorageNodeAsync(query.StorageNodeId, cancellationToken)
            .ConfigureAwait(false);
        if (primaryNode is null)
        {
            return Option.From(QuorumReplicateRepositoryResult.Failed("Primary storage node is unknown."));
        }

        if (query.ExpectedPrimaryLastSyncedAt is null)
        {
            // A push is replicating now: keep the reconciler off this repository meanwhile.
            await MarkPrimaryReplicatingAsync(context.RepositoryId, query.StorageNodeId.Value, cancellationToken)
                .ConfigureAwait(false);
        }

        var artifact = await _artifactPublisher
            .CreateOnPrimaryAsync(
                primaryNode,
                context.RepositoryId,
                context.PhysicalPath,
                query.AppliedWatermark,
                context.ReplicationEpoch,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (!artifact.Success)
        {
            return Option.From(
                QuorumReplicateRepositoryResult.Failed(
                    $"Encrypted artifact could not be created on the primary: {Describe(artifact.StatusCode, artifact.Error)}"
                )
            );
        }

        var quorumNodeIds = new List<StorageNodeId> { query.StorageNodeId };
        var refusedPeers = new List<RepositoryReplicationPeerDto>();
        string? firstUploadError = null;
        foreach (var peer in encryptedPeers)
        {
            if (
                query.ExpectedPrimaryLastSyncedAt is { } claimed
                && !await PrimaryStillClaimedAsync(
                        context.RepositoryId,
                        query.StorageNodeId.Value,
                        claimed,
                        cancellationToken
                    )
                    .ConfigureAwait(false)
            )
            {
                // A push started replicating since the reconciler claimed this repository. Its
                // artifact is newer than ours, so ours must not land after it under the same
                // watermark: stop here and leave the repository to the push. Checked before
                // every upload, so only one upload can be in flight when a push starts.
                return Option.From(
                    QuorumReplicateRepositoryResult.Failed(
                        "A push started replicating this repository; left to the push."
                    )
                );
            }

            var node = await LoadStorageNodeAsync(StorageNodeId.From(peer.StorageNodeId), cancellationToken)
                .ConfigureAwait(false);
            if (node is null)
            {
                refusedPeers.Add(peer);
                continue;
            }

            var upload = await _artifactPublisher
                .UploadAsync(node, context.RepositoryId, query.AppliedWatermark, artifact, cancellationToken)
                .ConfigureAwait(false);
            if (upload.Success)
            {
                quorumNodeIds.Add(node.Id);
            }
            else
            {
                refusedPeers.Add(peer);
                firstUploadError ??= $"{node.NodeId}: {Describe(upload.StatusCode, upload.Error)}";
            }
        }

        if (quorumNodeIds.Count < 2)
        {
            return Option.From(
                QuorumReplicateRepositoryResult.Failed(
                    $"No encrypted replica stored the artifact ({firstUploadError ?? "no reachable replica"})."
                )
            );
        }

        var commitResult = await _queryProcessor
            .RunQueryAsync(
                new CommitReplicationWatermarkQuery
                {
                    RepositoryId = query.RepositoryId,
                    StorageNodeId = query.StorageNodeId,
                    ReplicationEpoch = context.ReplicationEpoch,
                    NewWatermark = query.AppliedWatermark,
                    QuorumNodeIds = quorumNodeIds,
                },
                cancellationToken
            )
            .ConfigureAwait(false);
        if (commitResult.IsNone || !commitResult.Get().Success)
        {
            return Option.From(
                await CommitFailureAsync(query, commitResult, cancellationToken).ConfigureAwait(false)
            );
        }

        var primaryPeer = context.Peers.First(peer =>
            string.Equals(peer.Role, nameof(RepositoryReplicaRole.Primary), StringComparison.Ordinal)
        );
        var readReplicaPeer = context.Peers.FirstOrDefault(peer =>
            peer.IsHealthy
            && string.Equals(peer.Role, nameof(RepositoryReplicaRole.ReadReplica), StringComparison.Ordinal)
        );
        _ = Task.Run(
            () =>
                CatchUpRf4Async(
                    context.RepositoryId,
                    context.PhysicalPath,
                    primaryPeer.InternalHost,
                    readReplicaPeer,
                    refusedPeers,
                    quorumNodeIds[1].Value,
                    query.AppliedWatermark,
                    artifact,
                    CancellationToken.None
                ),
            CancellationToken.None
        );

        return Option.From(
            QuorumReplicateRepositoryResult.Replicated(commitResult.Get().PrimaryWatermark)
        );
    }

    /// <summary>
    /// A failed commit is stale when the watermark has meanwhile been committed by someone else
    /// (another push, or the reconciler on either API instance): the caller should retry with the
    /// next watermark, because the winning commit may not contain its push.
    /// </summary>
    private async Task<QuorumReplicateRepositoryResult> CommitFailureAsync(
        QuorumReplicateRepositoryQuery query,
        Option<CommitReplicationWatermarkResult> commitResult,
        CancellationToken cancellationToken
    )
    {
        var error = commitResult.IsSome ? commitResult.Get().Error : null;
        var current = await _queryProcessor
            .RunQueryAsync(
                new GetRepositoryReplicationContextQuery
                {
                    RepositoryId = query.RepositoryId,
                    StorageNodeId = query.StorageNodeId,
                },
                cancellationToken
            )
            .ConfigureAwait(false);
        if (current.IsSome && current.Get().PrimaryWatermark >= query.AppliedWatermark)
        {
            return QuorumReplicateRepositoryResult.StaleWatermark(
                $"Watermark {query.AppliedWatermark} was committed concurrently."
            );
        }

        return QuorumReplicateRepositoryResult.Failed(error ?? "Watermark commit failed.");
    }

    private async Task MarkPrimaryReplicatingAsync(
        Guid repositoryId,
        Guid primaryNodeId,
        CancellationToken cancellationToken
    )
    {
        var now = ReplicationClaim.Now();
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        await db
            .Set<RepositoryReplicaEntity>()
            .Where(replica => replica.RepositoryId == repositoryId && replica.StorageNodeId == primaryNodeId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(replica => replica.LastSyncedAt, now), cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<bool> PrimaryStillClaimedAsync(
        Guid repositoryId,
        Guid primaryNodeId,
        DateTimeOffset claimed,
        CancellationToken cancellationToken
    )
    {
        await using var db = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var lastSyncedAt = await db
            .Set<RepositoryReplicaEntity>()
            .AsNoTracking()
            .Where(replica => replica.RepositoryId == repositoryId && replica.StorageNodeId == primaryNodeId)
            .Select(replica => replica.LastSyncedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        return lastSyncedAt == claimed;
    }

    private async Task CatchUpRf4Async(
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
            var readNode = await LoadStorageNodeAsync(
                    StorageNodeId.From(readReplicaPeer.StorageNodeId),
                    cancellationToken
                )
                .ConfigureAwait(false);
            var readToken = readNode is null
                ? null
                : await GetApiTokenAsync(readNode.Id, cancellationToken).ConfigureAwait(false);
            if (readNode is not null && readToken is not null)
            {
                await _storageProvisionerClient
                    .SyncRepositoryFromPeerAsync(
                        readNode,
                        readToken,
                        physicalPath,
                        primaryHost,
                        physicalPath,
                        MtlsGitHttpPort,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }
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
                // Left lagging on purpose: the reconciler repairs replicas below PrimaryWatermark.
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

    private async Task<Option<QuorumReplicateRepositoryResult>> RunRf3QuorumAsync(
        QuorumReplicateRepositoryQuery query,
        RepositoryReplicationContextDto context,
        CancellationToken cancellationToken
    )
    {
        var primaryPeer = context.Peers.FirstOrDefault(peer =>
            string.Equals(peer.Role, nameof(RepositoryReplicaRole.Primary), StringComparison.Ordinal)
        );
        if (primaryPeer is null)
        {
            return Option.From(
                QuorumReplicateRepositoryResult.Failed("Primary peer metadata is missing.")
            );
        }

        var healthyPeers = context.Peers.Where(peer => peer.IsHealthy).ToList();
        if (healthyPeers.Count < 2)
        {
            return Option.From(
                QuorumReplicateRepositoryResult.Failed(
                    "Write quorum unavailable: fewer than two trio members are healthy."
                )
            );
        }

        var replicaPeers = healthyPeers
            .Where(peer =>
                !string.Equals(
                    peer.Role,
                    nameof(RepositoryReplicaRole.Primary),
                    StringComparison.Ordinal
                )
                && !string.Equals(
                    peer.Role,
                    nameof(RepositoryReplicaRole.EncryptedReplica),
                    StringComparison.Ordinal
                )
            )
            .ToList();

        var syncedNodeIds = new List<StorageNodeId> { query.StorageNodeId };
        var remainingReplicaPeers = new List<RepositoryReplicationPeerDto>();

        foreach (var replicaPeer in replicaPeers)
        {
            var replicaNodeId = StorageNodeId.From(replicaPeer.StorageNodeId);
            if (syncedNodeIds.Contains(replicaNodeId))
            {
                continue;
            }

            var replicaNode = await LoadStorageNodeAsync(replicaNodeId, cancellationToken)
                .ConfigureAwait(false);
            if (replicaNode is null)
            {
                continue;
            }

            var token = await GetApiTokenAsync(replicaNode.Id, cancellationToken)
                .ConfigureAwait(false);
            if (token is null)
            {
                continue;
            }

            StorageProvisionerResult syncResult;
            try
            {
                syncResult = await _storageProvisionerClient
                    .SyncRepositoryFromPeerAsync(
                        replicaNode,
                        token,
                        context.PhysicalPath,
                        primaryPeer.InternalHost,
                        context.PhysicalPath,
                        MtlsGitHttpPort,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                remainingReplicaPeers.Add(replicaPeer);
                continue;
            }

            if (!syncResult.Success)
            {
                remainingReplicaPeers.Add(replicaPeer);
                continue;
            }

            syncedNodeIds.Add(replicaNode.Id);
            if (syncedNodeIds.Count >= 2)
            {
                remainingReplicaPeers.AddRange(
                    replicaPeers.Where(peer =>
                        !syncedNodeIds.Contains(StorageNodeId.From(peer.StorageNodeId))
                    )
                );
                break;
            }
        }

        if (syncedNodeIds.Count < 2)
        {
            return Option.From(
                QuorumReplicateRepositoryResult.Failed(
                    "Write quorum unavailable: could not replicate to a second node."
                )
            );
        }

        var commitResult = await _queryProcessor
            .RunQueryAsync(
                new CommitReplicationWatermarkQuery
                {
                    RepositoryId = query.RepositoryId,
                    StorageNodeId = query.StorageNodeId,
                    ReplicationEpoch = context.ReplicationEpoch,
                    NewWatermark = query.AppliedWatermark,
                    QuorumNodeIds = syncedNodeIds,
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (commitResult.IsNone || !commitResult.Get().Success)
        {
            var error = commitResult.IsSome
                ? commitResult.Get().Error
                : "Watermark commit failed.";
            return Option.From(QuorumReplicateRepositoryResult.Failed(error!));
        }

        foreach (var remainingPeer in remainingReplicaPeers.DistinctBy(peer => peer.StorageNodeId))
        {
            var remainingNodeId = StorageNodeId.From(remainingPeer.StorageNodeId);
            _ = Task.Run(
                () =>
                    CatchUpReplicaAsync(
                        remainingNodeId,
                        context.PhysicalPath,
                        primaryPeer.InternalHost,
                        CancellationToken.None
                    ),
                CancellationToken.None
            );
        }

        return Option.From(
            QuorumReplicateRepositoryResult.Replicated(commitResult.Get().PrimaryWatermark)
        );
    }

    private async Task CatchUpReplicaAsync(
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

    private async Task<StorageNodeDto?> LoadStorageNodeAsync(
        StorageNodeId storageNodeId,
        CancellationToken cancellationToken
    )
    {
        var result = await _queryProcessor
            .RunQueryAsync(
                new GetStorageNodeQuery { ModelId = storageNodeId },
                cancellationToken
            )
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
