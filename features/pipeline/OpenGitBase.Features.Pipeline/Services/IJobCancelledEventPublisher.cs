namespace OpenGitBase.Features.Pipeline.Services;

public interface IJobCancelledEventPublisher
{
    Task PublishCancelledAsync(Guid jobId, CancellationToken cancellationToken);
}
