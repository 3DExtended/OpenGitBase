namespace OpenGitBase.Api.Services;

public interface IRepositoryKeyService
{
    Task<int> GenerateAndStoreKeyAsync(Guid repositoryId, CancellationToken cancellationToken = default);

    Task<byte[]?> TryGetEphemeralKeyForPrimaryAsync(
        Guid repositoryId,
        Guid callerStorageNodeId,
        CancellationToken cancellationToken = default
    );
}
