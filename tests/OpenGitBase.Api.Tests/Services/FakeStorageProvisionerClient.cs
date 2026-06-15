using OpenGitBase.Api.Services;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Tests.Services;

internal sealed class FakeStorageProvisionerClient : IStorageProvisionerClient
{
    public Task<StorageProvisionerResult> ProvisionRepositoryAsync(
        StorageNodeDto node,
        string apiToken,
        string physicalPath,
        CancellationToken cancellationToken
    ) => Task.FromResult(StorageProvisionerResult.Ok(201));

    public Task<StorageProvisionerResult> DeleteRepositoryAsync(
        StorageNodeDto node,
        string apiToken,
        string physicalPath,
        CancellationToken cancellationToken
    ) => Task.FromResult(StorageProvisionerResult.Ok(200));
}
