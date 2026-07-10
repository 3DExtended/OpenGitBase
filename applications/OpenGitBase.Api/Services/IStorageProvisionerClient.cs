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
}
