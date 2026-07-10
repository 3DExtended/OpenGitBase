namespace OpenGitBase.Api.Services;

public interface IRepositoryKeyService
{
    Task<int> GenerateAndStoreKeyAsync(Guid repositoryId, CancellationToken cancellationToken = default);

    Task<EphemeralRepositoryKey?> TryGetEphemeralKeyForPrimaryAsync(
        Guid repositoryId,
        Guid callerStorageNodeId,
        CancellationToken cancellationToken = default
    );

    Task<EphemeralRepositoryKey?> TryGetRepositoryKeyAsync(
        Guid repositoryId,
        CancellationToken cancellationToken = default
    );
}
