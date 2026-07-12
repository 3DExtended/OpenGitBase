namespace OpenGitBase.Features.Pipeline.Services;

public sealed class NoopPipelineEventPublisher
    : IGitPushEventPublisher,
        IJobAvailableEventPublisher,
        IJobCancelledEventPublisher
{
    public Task PublishAsync(
        Guid repositoryId,
        string @ref,
        string afterSha,
        CancellationToken cancellationToken
    ) => Task.CompletedTask;

    public Task PublishAsync(Guid jobId, CancellationToken cancellationToken) => Task.CompletedTask;

    public Task PublishCancelledAsync(Guid jobId, CancellationToken cancellationToken) => Task.CompletedTask;
}
