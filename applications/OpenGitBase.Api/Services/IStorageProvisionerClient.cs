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
