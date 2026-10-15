namespace OpenGitBase.Api.Services;

/// <summary>
/// Runs <see cref="LfsReplicationService.CompleteCopiesAsync"/> on a thread-pool thread in a DI scope of its own:
/// the verify request's scope is gone by the time the remaining copies finish.
/// </summary>
public sealed class LfsCopyCompletionScheduler : ILfsCopyCompletionScheduler
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LfsCopyCompletionScheduler> _logger;

    public LfsCopyCompletionScheduler(IServiceScopeFactory scopeFactory, ILogger<LfsCopyCompletionScheduler> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void Schedule(LfsPendingCopies pending) =>
        _ = Task.Run(
            async () =>
            {
                try
                {
                    await using var scope = _scopeFactory.CreateAsyncScope();
                    await scope
                        .ServiceProvider.GetRequiredService<LfsReplicationService>()
                        .CompleteCopiesAsync(pending, CancellationToken.None)
                        .ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Completing the copies of LFS object {Oid} failed; the reconciler repairs missing copies.",
                        pending.Oid
                    );
                }
            },
            CancellationToken.None
        );
}
