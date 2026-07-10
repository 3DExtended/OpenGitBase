using OpenGitBase.Api.Services;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Tests.Services;

internal sealed class FakeStorageProvisionerClient : IStorageProvisionerClient
{
    public Task<StorageProvisionerResult> ProvisionRepositoryAsync(
        StorageNodeDto node,
        string apiToken,
        string physicalPath,
        long receiveMaxBytes,
        string replicationRole = "Primary",
        CancellationToken cancellationToken = default
    ) => Task.FromResult(StorageProvisionerResult.Ok(201));

    public Task<StorageProvisionerResult> DeleteRepositoryAsync(
        StorageNodeDto node,
        string apiToken,
        string physicalPath,
        CancellationToken cancellationToken
    ) => Task.FromResult(StorageProvisionerResult.Ok(200));

    public Task<StorageProvisionerResult> SyncRepositoryFromPeerAsync(
        StorageNodeDto node,
        string apiToken,
        string physicalPath,
        string sourceHost,
        string sourcePhysicalPath,
        int sourcePort,
        CancellationToken cancellationToken
    ) => Task.FromResult(StorageProvisionerResult.Ok(200));

    public Task<StorageProvisionerResult> UploadReplicationArtifactAsync(
        StorageNodeDto node,
        string apiToken,
        Guid repositoryId,
        long watermark,
        string manifestJson,
        byte[] bundlePayload,
        CancellationToken cancellationToken = default
    ) => Task.FromResult(StorageProvisionerResult.Ok(201));

    public Task<ReplicationArtifactFetchResult> TryGetReplicationArtifactAsync(
        StorageNodeDto node,
        string apiToken,
        Guid repositoryId,
        long watermark,
        CancellationToken cancellationToken = default
    ) =>
        Task.FromResult(
            ReplicationArtifactFetchResult.Ok("{\"epoch\":0,\"watermark\":0,\"bundleSha256\":\"ABC\",\"keyVersion\":1}", [])
        );

    public Task<StorageProvisionerResult> DeleteReplicationArtifactAsync(
        StorageNodeDto node,
        string apiToken,
        Guid repositoryId,
        long watermark,
        CancellationToken cancellationToken = default
    ) => Task.FromResult(StorageProvisionerResult.Ok(200));

    public Task<StorageProvisionerResult> ImportRepositoryBundleAsync(
        StorageNodeDto node,
        string apiToken,
        string physicalPath,
        byte[] bundlePlaintext,
        CancellationToken cancellationToken = default
    ) => Task.FromResult(StorageProvisionerResult.Ok(201));
}
