using System.Collections.Concurrent;
using System.Threading.Channels;

namespace OpenGitBase.Api.Services;

/// <summary>
/// Runs LFS reconcile runs one repository at a time, each in a DI scope of its own, outside the reconciler pass.
/// That pass runs in the HA background loop between failover checks, and copying large objects must not hold
/// the loop up.
/// <para>
/// Requests for a repository that is already waiting collapse into one, so a clone that misses many objects asks
/// for a single run. A run that repaired some copies but stopped at <c>Lfs:MaxRepairsPerRun</c> queues the
/// repository again behind the others. A run that repaired nothing waits for the next request, so a copy that
/// cannot be repaired does not make the worker spin. Each API instance runs its own worker. When two instances
/// repair the same copy, each writes a valid copy and storage keeps whichever lands last.
/// </para>
/// </summary>
public sealed class LfsRepairQueue : BackgroundService, ILfsRepairQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>(new UnboundedChannelOptions { SingleReader = true });
    private readonly ConcurrentDictionary<Guid, byte> _waiting = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LfsRepairQueue> _logger;

    public LfsRepairQueue(IServiceScopeFactory scopeFactory, ILogger<LfsRepairQueue> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public void Schedule(Guid repositoryId)
    {
        if (_waiting.TryAdd(repositoryId, 0))
        {
            _channel.Writer.TryWrite(repositoryId);
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var repositoryId in _channel.Reader.ReadAllAsync(stoppingToken).ConfigureAwait(false))
            {
                // A request that arrives while this run is under way queues another run: it may have seen a newer loss.
                _waiting.TryRemove(repositoryId, out _);
                await RunAsync(repositoryId, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Shutting down; waiting repositories are picked up by the next reconciler pass.
        }
    }

    private async Task RunAsync(Guid repositoryId, CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var result = await scope
                .ServiceProvider.GetRequiredService<ILfsCopyReconciler>()
                .ReconcileRepositoryAsync(repositoryId, stoppingToken)
                .ConfigureAwait(false);
            if (result.Missing > 0)
            {
                _logger.LogInformation(
                    "LFS reconcile of {RepositoryId}: {Missing} copies missing, {Repaired} repaired, {Failed} failed, {Remaining} left for the next run",
                    repositoryId,
                    result.Missing,
                    result.Repaired,
                    result.Failed,
                    result.Remaining
                );
            }

            if (result.Remaining > 0 && result.Repaired > 0)
            {
                Schedule(repositoryId);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "LFS reconcile of {RepositoryId} failed; the next request or reconciler pass retries it", repositoryId);
        }
    }
}
