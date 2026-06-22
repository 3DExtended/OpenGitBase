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

    public QuorumReplicateRepositoryQueryHandler(
        IQueryProcessor queryProcessor,
        IStorageProvisionerClient storageProvisionerClient
    )
    {
        _queryProcessor = queryProcessor;
        _storageProvisionerClient = storageProvisionerClient;
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

        if (query.AppliedWatermark != context.PrimaryWatermark + 1)
        {
            return Option.From(
                QuorumReplicateRepositoryResult.Failed(
                    "Applied watermark must be exactly one greater than the primary watermark."
                )
            );
        }

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
                () => CatchUpReplicaAsync(
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
