namespace OpenGitBase.Common.Services;

public interface IRepositoryKeyRotationService
{
    /// <summary>
    /// Rotates the repository key. Not implemented in v1.
    /// </summary>
    Task<int> RotateKeyAsync(Guid repositoryId, CancellationToken cancellationToken = default);
}
