namespace OpenGitBase.Api.Services;

public interface IColdRecoveryService
{
    Task<bool> TryRecoverAsync(Guid repositoryId, CancellationToken cancellationToken = default);
}
