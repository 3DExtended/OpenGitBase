namespace OpenGitBase.Common.Services;

public class RepositoryKeyRotationService : IRepositoryKeyRotationService
{
    public Task<int> RotateKeyAsync(Guid repositoryId, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("Repository key rotation is not implemented yet.");
}
