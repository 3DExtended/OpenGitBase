namespace OpenGitBase.Features.Pipeline.Services;

public sealed class NoopPipelineEventPublisher : IGitPushEventPublisher, IJobAvailableEventPublisher
{
    public Task PublishAsync(
        Guid repositoryId,
        string @ref,
        string afterSha,
        CancellationToken cancellationToken
    ) => Task.CompletedTask;

    public Task PublishAsync(Guid jobId, CancellationToken cancellationToken) => Task.CompletedTask;
}
