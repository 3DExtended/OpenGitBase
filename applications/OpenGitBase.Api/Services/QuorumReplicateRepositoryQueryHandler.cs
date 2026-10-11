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
    private readonly IServiceScopeFactory _scopeFactory;

    public QuorumReplicateRepositoryQueryHandler(
        IQueryProcessor queryProcessor,
        IStorageProvisionerClient storageProvisionerClient,
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        ReplicationArtifactPublisher artifactPublisher,
        IServiceScopeFactory scopeFactory
    )
    {
        _queryProcessor = queryProcessor;
        _storageProvisionerClient = storageProvisionerClient;
        _contextFactory = contextFactory;
        _artifactPublisher = artifactPublisher;
        _scopeFactory = scopeFactory;
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

        if (query.ExpectedPrimaryLastSyncedAt is null)
        {
            // A push is replicating now: the reconciler leaves this repository alone meanwhile,
            // and a reconciler run that claimed it stops before its next upload, sync or commit.
            await MarkPrimaryReplicatingAsync(context.RepositoryId, query.StorageNodeId.Value, cancellationToken)
                .ConfigureAwait(false);
        }

        Option<QuorumReplicateRepositoryResult> result;
        if (string.Equals(context.ReplicationState, nameof(ReplicationState.Rf4Healthy), StringComparison.Ordinal))
        {
            result = query.CreateArtifact
                ? await RunRf4CreateArtifactQuorumAsync(query, context, cancellationToken).ConfigureAwait(false)
                : await RunRf4QuorumAsync(query, context, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            result = await RunRf3QuorumAsync(query, context, cancellationToken).ConfigureAwait(false);
        }

        return await StaleIfCommittedMeanwhileAsync(query, result, cancellationToken).ConfigureAwait(false);
    }

    private static string Describe(int statusCode, string? error)
    {
        var detail = string.IsNullOrWhiteSpace(error) ? "no detail" : error.Trim();
        return statusCode > 0 ? $"HTTP {statusCode}: {detail}" : detail;
    }

    private static QuorumReplicateRepositoryResult LeftToThePush() =>
        QuorumReplicateRepositoryResult.Failed("A push started replicating this repository; left to the push.");

    /// <summary>
    /// Any failure, however it came about (lost compare-and-set, failed create or upload, a claim
    /// a push took over), is reported as stale once the watermark has been committed by someone
    /// else meanwhile (another push, or the reconciler on either API instance). That commit may
    /// not hold the caller's push, and the caller's watermark file now equals PrimaryWatermark, so
    /// nothing would pick the push up: stale makes the hook retry with the next watermark.
    /// </summary>
    private async Task<Option<QuorumReplicateRepositoryResult>> StaleIfCommittedMeanwhileAsync(
        QuorumReplicateRepositoryQuery query,
        Option<QuorumReplicateRepositoryResult> result,
        CancellationToken cancellationToken
    )
    {
        if (result.IsSome && (result.Get().Success || result.Get().Stale))
        {
            return result;
        }

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
            return Option.From(
                QuorumReplicateRepositoryResult.StaleWatermark(
                    $"Watermark {query.AppliedWatermark} was committed concurrently."
                )
            );
        }

        return result;
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

        var sourceEncryptedNodeId = confirmedEncryptedNodeIds[0];
        ReplicaCatchUpService.StartInBackground(
            _scopeFactory,
            catchUp =>
                catchUp.CatchUpRf4Async(
                    context.RepositoryId,
                    context.PhysicalPath,
                    primaryPeer.InternalHost,
                    readReplicaPeer,
                    remainingEncryptedPeers,
                    sourceEncryptedNodeId,
                    query.AppliedWatermark,
                    artifactInHand: null,
                    CancellationToken.None
                )
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
    /// the fan-out (<see cref="ReplicaCatchUpService"/>) with the artifact still in hand.
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

        IReadOnlyList<PeerUpload> uploads;
        if (query.ExpectedPrimaryLastSyncedAt is { } claimed)
        {
            var whileClaimed = await UploadWhileClaimedAsync(query, context, encryptedPeers, artifact, claimed, cancellationToken)
                .ConfigureAwait(false);
            if (whileClaimed is null)
            {
                // A push started replicating since the reconciler claimed this repository. Its
                // artifact is newer than ours, so ours must not land after it under the same
                // watermark: stop here and leave the repository to the push.
                return Option.From(LeftToThePush());
            }

            uploads = whileClaimed;
        }
        else
        {
            // A push: every replica at once, each one time-boxed by the publisher, so one replica
            // that does not answer neither delays the others nor runs this request into
            // HAProxy's timeout.
            uploads = await Task.WhenAll(
                    encryptedPeers.Select(peer =>
                        UploadToPeerAsync(peer, context.RepositoryId, query.AppliedWatermark, artifact, cancellationToken)
                    )
                )
                .ConfigureAwait(false);
        }

        var quorumNodeIds = new List<StorageNodeId> { query.StorageNodeId };
        quorumNodeIds.AddRange(
            uploads.Where(upload => upload.Result.Success).Select(upload => StorageNodeId.From(upload.Peer.StorageNodeId))
        );
        var refusedPeers = uploads.Where(upload => !upload.Result.Success).Select(upload => upload.Peer).ToList();
        var firstUploadError = uploads
            .Where(upload => !upload.Result.Success)
            .Select(upload => $"{upload.NodeName}: {Describe(upload.Result.StatusCode, upload.Result.Error)}")
            .FirstOrDefault();

        if (quorumNodeIds.Count < 2)
        {
            return Option.From(
                QuorumReplicateRepositoryResult.Failed(
                    $"No encrypted replica stored the artifact ({firstUploadError ?? "no reachable replica"})."
                )
            );
        }

        if (!await StillClaimedAsync(query, cancellationToken).ConfigureAwait(false))
        {
            return Option.From(LeftToThePush());
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
                QuorumReplicateRepositoryResult.Failed(
                    commitResult.IsSome ? commitResult.Get().Error ?? "Watermark commit failed." : "Watermark commit failed."
                )
            );
        }

        var primaryPeer = context.Peers.First(peer =>
            string.Equals(peer.Role, nameof(RepositoryReplicaRole.Primary), StringComparison.Ordinal)
        );
        var readReplicaPeer = context.Peers.FirstOrDefault(peer =>
            peer.IsHealthy
            && string.Equals(peer.Role, nameof(RepositoryReplicaRole.ReadReplica), StringComparison.Ordinal)
        );
        var sourceNodeId = quorumNodeIds[1].Value;
        ReplicaCatchUpService.StartInBackground(
            _scopeFactory,
            catchUp =>
                catchUp.CatchUpRf4Async(
                    context.RepositoryId,
                    context.PhysicalPath,
                    primaryPeer.InternalHost,
                    readReplicaPeer,
                    refusedPeers,
                    sourceNodeId,
                    query.AppliedWatermark,
                    artifact,
                    CancellationToken.None
                )
        );

        return Option.From(
            QuorumReplicateRepositoryResult.Replicated(commitResult.Get().PrimaryWatermark)
        );
    }

    /// <summary>
    /// Reconciler path: one replica after the other, and before each one a check that no push
    /// has marked the primary since the claim, so at most one upload of the reconciler's (older)
    /// artifact can be in flight when a push starts. Null when a push took over.
    /// </summary>
    private async Task<IReadOnlyList<PeerUpload>?> UploadWhileClaimedAsync(
        QuorumReplicateRepositoryQuery query,
        RepositoryReplicationContextDto context,
        IReadOnlyList<RepositoryReplicationPeerDto> encryptedPeers,
        ReplicationArtifactFetchResult artifact,
        DateTimeOffset claimed,
        CancellationToken cancellationToken
    )
    {
        var uploads = new List<PeerUpload>();
        foreach (var peer in encryptedPeers)
        {
            if (
                !await PrimaryStillClaimedAsync(context.RepositoryId, query.StorageNodeId.Value, claimed, cancellationToken)
                    .ConfigureAwait(false)
            )
            {
                return null;
            }

            uploads.Add(
                await UploadToPeerAsync(peer, context.RepositoryId, query.AppliedWatermark, artifact, cancellationToken)
                    .ConfigureAwait(false)
            );
        }

        return uploads;
    }

    private async Task<PeerUpload> UploadToPeerAsync(
        RepositoryReplicationPeerDto peer,
        Guid repositoryId,
        long watermark,
        ReplicationArtifactFetchResult artifact,
        CancellationToken cancellationToken
    )
    {
        var node = await LoadStorageNodeAsync(StorageNodeId.From(peer.StorageNodeId), cancellationToken)
            .ConfigureAwait(false);
        if (node is null)
        {
            return new PeerUpload(peer, peer.StorageNodeId.ToString(), StorageProvisionerResult.Fail(404, "unknown storage node"));
        }

        var result = await _artifactPublisher
            .UploadAsync(node, repositoryId, watermark, artifact, cancellationToken)
            .ConfigureAwait(false);
        return new PeerUpload(peer, node.NodeId, result);
    }

    /// <summary>
    /// True for a push, and for a reconciler run while no push has marked the primary since its
    /// claim.
    /// </summary>
    private async Task<bool> StillClaimedAsync(
        QuorumReplicateRepositoryQuery query,
        CancellationToken cancellationToken
    ) =>
        query.ExpectedPrimaryLastSyncedAt is not { } claimed
        || await PrimaryStillClaimedAsync(query.RepositoryId.Value, query.StorageNodeId.Value, claimed, cancellationToken)
            .ConfigureAwait(false);

    private Task<bool> MarkPrimaryReplicatingAsync(
        Guid repositoryId,
        Guid primaryNodeId,
        CancellationToken cancellationToken
    ) => ReplicationClaim.MarkPrimaryReplicatingAsync(_contextFactory, repositoryId, primaryNodeId, cancellationToken);

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

            if (!await StillClaimedAsync(query, cancellationToken).ConfigureAwait(false))
            {
                return Option.From(LeftToThePush());
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

        if (!await StillClaimedAsync(query, cancellationToken).ConfigureAwait(false))
        {
            return Option.From(LeftToThePush());
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
            ReplicaCatchUpService.StartInBackground(
                _scopeFactory,
                catchUp =>
                    catchUp.CatchUpReplicaAsync(
                        remainingNodeId,
                        context.PhysicalPath,
                        primaryPeer.InternalHost,
                        CancellationToken.None
                    )
            );
        }

        return Option.From(
            QuorumReplicateRepositoryResult.Replicated(commitResult.Get().PrimaryWatermark)
        );
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

    private sealed record PeerUpload(RepositoryReplicationPeerDto Peer, string NodeName, StorageProvisionerResult Result);
}
