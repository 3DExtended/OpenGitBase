namespace OpenGitBase.Features.Pipeline.Services;

public interface IJobAvailableEventPublisher
{
    Task PublishAsync(Guid jobId, CancellationToken cancellationToken);
}
