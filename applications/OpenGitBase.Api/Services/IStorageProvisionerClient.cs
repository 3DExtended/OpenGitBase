using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Services;

public interface IStorageProvisionerClient
{
    Task<StorageProvisionerResult> ProvisionRepositoryAsync(
        StorageNodeDto node,
        string apiToken,
        string physicalPath,
        long receiveMaxBytes,
        string replicationRole = "Primary",
        CancellationToken cancellationToken = default
    );

    Task<StorageProvisionerResult> DeleteRepositoryAsync(
        StorageNodeDto node,
        string apiToken,
        string physicalPath,
        CancellationToken cancellationToken
    );

    Task<StorageProvisionerResult> SyncRepositoryFromPeerAsync(
        StorageNodeDto node,
        string apiToken,
        string physicalPath,
        string sourceHost,
        string sourcePhysicalPath,
        int sourcePort,
        CancellationToken cancellationToken
    );

    Task<StorageProvisionerResult> UploadReplicationArtifactAsync(
        StorageNodeDto node,
        string apiToken,
        Guid repositoryId,
        long watermark,
        string manifestJson,
        byte[] bundlePayload,
        CancellationToken cancellationToken = default
    );

    Task<ReplicationArtifactFetchResult> TryGetReplicationArtifactAsync(
        StorageNodeDto node,
        string apiToken,
        Guid repositoryId,
        long watermark,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Ask an encrypted-replica storage node for its true on-disk artifact watermark (the highest
    /// watermark with a complete artifact present), used to detect false-healthy replicas whose DB
    /// <c>ArtifactWatermark</c> is ahead of what is actually stored.
    /// </summary>
    Task<ArtifactWatermarkStatusResult> TryGetArtifactWatermarkStatusAsync(
        StorageNodeDto node,
        string apiToken,
        Guid repositoryId,
        CancellationToken cancellationToken = default
    );

    Task<ReplicationArtifactFetchResult> CreateReplicationArtifactAsync(
        StorageNodeDto node,
        string apiToken,
        string physicalPath,
        Guid repositoryId,
        long watermark,
        long epoch,
        string keyHex,
        int keyVersion,
        CancellationToken cancellationToken = default
    );

    Task<StorageProvisionerResult> DeleteReplicationArtifactAsync(
        StorageNodeDto node,
        string apiToken,
        Guid repositoryId,
        long watermark,
        CancellationToken cancellationToken = default
    );

    Task<StorageProvisionerResult> ImportRepositoryBundleAsync(
        StorageNodeDto node,
        string apiToken,
        string physicalPath,
        byte[] bundlePlaintext,
        CancellationToken cancellationToken = default
    );
}
