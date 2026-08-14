using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Api.Services;

public sealed class HaStorageBackgroundService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly HaStorageBackgroundOptions _options;
    private readonly ILogger<HaStorageBackgroundService> _logger;

    public HaStorageBackgroundService(
        IServiceScopeFactory scopeFactory,
        IOptions<HaStorageBackgroundOptions> options,
        ILogger<HaStorageBackgroundService> logger
    )
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var failoverDue = DateTimeOffset.UtcNow;
        var backfillDue = DateTimeOffset.UtcNow;
        var rebalanceDue = DateTimeOffset.UtcNow;
        var reconcileDue = DateTimeOffset.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            var now = DateTimeOffset.UtcNow;

            // Each sub-job is isolated behind its own try/catch and advances its own "due"
            // timestamp before running. A failure in one (e.g. one repo's promotion throwing)
            // must not starve the other three, which a single shared catch around all four did.
            if (now >= failoverDue)
            {
                failoverDue = now.AddSeconds(_options.FailoverIntervalSeconds);
                await RunCycleAsync("failover", RunFailoverAsync, stoppingToken).ConfigureAwait(false);
            }

            if (now >= backfillDue)
            {
                backfillDue = now.AddSeconds(_options.BackfillIntervalSeconds);
                await RunCycleAsync("rf1-backfill", RunRf1BackfillAsync, stoppingToken).ConfigureAwait(false);
                await RunCycleAsync("rf4-backfill", RunRf4BackfillAsync, stoppingToken).ConfigureAwait(false);
            }

            if (now >= rebalanceDue)
            {
                rebalanceDue = now.AddSeconds(_options.RebalanceIntervalSeconds);
                await RunCycleAsync("rebalance", RunRebalanceAsync, stoppingToken).ConfigureAwait(false);
            }

            if (now >= reconcileDue)
            {
                reconcileDue = now.AddSeconds(_options.ReconcilerIntervalSeconds);
                await RunCycleAsync("reconcile", RunReconcilerAsync, stoppingToken).ConfigureAwait(false);
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RunCycleAsync(
        string cycleName,
        Func<CancellationToken, Task> cycle,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await cycle(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "HA storage {Cycle} cycle failed.", cycleName);
        }
    }

    private async Task RunFailoverAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var environment = scope.ServiceProvider.GetRequiredService<IHostEnvironment>();
        if (environment.IsEnvironment("E2ETest"))
        {
            return;
        }

        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        var queryProcessor = scope.ServiceProvider.GetRequiredService<IQueryProcessor>();

        await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
        var repositories = await context
            .Set<RepositoryEntity>()
            .AsNoTracking()
            .Where(repository => repository.PrimaryStorageNodeId != null)
            .Select(repository => repository.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var repositoryId in repositories)
        {
            await queryProcessor
                .RunQueryAsync(
                    new PromotePrimaryReplicaQuery
                    {
                        RepositoryId = RepositoryId.From(repositoryId),
                    },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
    }

    private async Task RunRf1BackfillAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var backfill = scope.ServiceProvider.GetRequiredService<Rf1BackfillService>();
        await backfill.RunOnceAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RunRf4BackfillAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var backfill = scope.ServiceProvider.GetRequiredService<Rf4BackfillService>();
        await backfill.RunOnceAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RunRebalanceAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var rebalance = scope.ServiceProvider.GetRequiredService<RebalanceService>();
        await rebalance.RunOnceAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RunReconcilerAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var reconciler = scope.ServiceProvider.GetRequiredService<AntiEntropyReconcilerService>();
        await reconciler.RunOnceAsync(cancellationToken).ConfigureAwait(false);
    }
}
