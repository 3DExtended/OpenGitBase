using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Services;

/// <summary>
/// Uploads an encrypted replication artifact to a target encrypted replica on
/// behalf of the primary. The primary cannot authenticate to the replica's
/// internal HTTP endpoint (each node has its own per-node token), so the API
/// performs the upload using the target node's own token. This mirrors the
/// fan-out already performed by <see cref="QuorumReplicateRepositoryQueryHandler"/>
/// for the remaining encrypted replicas.
/// </summary>
public sealed class RelayReplicationArtifactQueryHandler
    : IQueryHandler<RelayReplicationArtifactQuery, RelayReplicationArtifactResult>
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly IStorageProvisionerClient _storageProvisionerClient;

    public RelayReplicationArtifactQueryHandler(
        IQueryProcessor queryProcessor,
        IStorageProvisionerClient storageProvisionerClient
    )
    {
        _queryProcessor = queryProcessor;
        _storageProvisionerClient = storageProvisionerClient;
    }

    public async Task<Option<RelayReplicationArtifactResult>> RunQueryAsync(
        RelayReplicationArtifactQuery query,
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
                RelayReplicationArtifactResult.Failed(
                    404,
                    "Repository replication context unavailable."
                )
            );
        }

        var context = contextResult.Get();
        if (!context.IsPrimary)
        {
            return Option.From(
                RelayReplicationArtifactResult.Failed(
                    403,
                    "Only the primary may relay replication artifacts."
                )
            );
        }

        var targetPeer = context.Peers.FirstOrDefault(peer =>
            peer.StorageNodeId == query.TargetStorageNodeId
            && peer.IsHealthy
            && string.Equals(
                peer.Role,
                nameof(RepositoryReplicaRole.EncryptedReplica),
                StringComparison.Ordinal
            )
        );
        if (targetPeer is null)
        {
            return Option.From(
                RelayReplicationArtifactResult.Failed(
                    409,
                    "Target node is not a healthy encrypted replica for this repository."
                )
            );
        }

        var targetNode = await LoadStorageNodeAsync(
                StorageNodeId.From(query.TargetStorageNodeId),
                cancellationToken
            )
            .ConfigureAwait(false);
        var targetToken = targetNode is null
            ? null
            : await GetApiTokenAsync(targetNode.Id, cancellationToken).ConfigureAwait(false);
        if (targetNode is null || targetToken is null)
        {
            return Option.From(
                RelayReplicationArtifactResult.Failed(
                    502,
                    "Target encrypted replica credentials are unavailable."
                )
            );
        }

        var upload = await _storageProvisionerClient
            .UploadReplicationArtifactAsync(
                targetNode,
                targetToken,
                context.RepositoryId,
                query.Watermark,
                query.ManifestJson,
                query.BundlePayload,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (!upload.Success)
        {
            return Option.From(
                RelayReplicationArtifactResult.Failed(
                    upload.StatusCode == 0 ? 502 : upload.StatusCode,
                    upload.Error ?? "Artifact relay upload failed."
                )
            );
        }

        return Option.From(RelayReplicationArtifactResult.Ok());
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
