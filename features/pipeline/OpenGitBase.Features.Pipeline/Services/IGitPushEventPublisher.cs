namespace OpenGitBase.Features.Pipeline.Services;

public interface IGitPushEventPublisher
{
    Task PublishAsync(Guid repositoryId, string @ref, string afterSha, CancellationToken cancellationToken);
}
