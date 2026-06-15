using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Services;

public interface IStorageProvisionerClient
{
    Task<StorageProvisionerResult> ProvisionRepositoryAsync(
        StorageNodeDto node,
        string apiToken,
        string physicalPath,
        long receiveMaxBytes,
        CancellationToken cancellationToken
    );

    Task<StorageProvisionerResult> DeleteRepositoryAsync(
        StorageNodeDto node,
        string apiToken,
        string physicalPath,
        CancellationToken cancellationToken
    );
}
