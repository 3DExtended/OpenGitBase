using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;
using OpenGitBase.Features.Pipeline.Services;

namespace OpenGitBase.Api.Services;

public sealed class JobDispatchCoordinator : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IJobAvailableEventPublisher _publisher;
    private readonly ILogger<JobDispatchCoordinator> _logger;

    public JobDispatchCoordinator(
        IServiceProvider serviceProvider,
        IJobAvailableEventPublisher publisher,
        ILogger<JobDispatchCoordinator> logger
    )
    {
        _serviceProvider = serviceProvider;
        _publisher = publisher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // An unhandled exception here would otherwise crash the whole API process
                // (all replicas independently), not just this feature. Log and retry next tick.
                _logger.LogError(ex, "Job dispatch cycle failed.");
            }

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RunCycleAsync(CancellationToken stoppingToken)
    {
        await using var scope = _serviceProvider.CreateAsyncScope();
        var contextFactory = scope.ServiceProvider.GetRequiredService<
            IDbContextFactory<OpenGitBaseDbContext>
        >();
        await using var context = await contextFactory
            .CreateDbContextAsync(stoppingToken)
            .ConfigureAwait(false);

        // Serialize across API replicas so both don't independently republish "job available"
        // for the same still-queued jobs on every tick.
        if (
            !await PostgresAdvisoryLockService
                .TryAcquireAsync(context, BackgroundWorkerAdvisoryLocks.JobDispatchCoordinator, stoppingToken)
                .ConfigureAwait(false)
        )
        {
            return;
        }

        try
        {
            var queuedJobs = await context
                .Set<PipelineJobEntity>()
                .Where(entity => entity.Status == PipelineJobStatus.Queued)
                .Take(25)
                .ToListAsync(stoppingToken)
                .ConfigureAwait(false);
            var queuedJobIds = queuedJobs
                .OrderBy(entity => entity.CreatedAt)
                .Select(entity => entity.Id)
                .ToList();

            foreach (var jobId in queuedJobIds)
            {
                await _publisher.PublishAsync(jobId, stoppingToken).ConfigureAwait(false);
            }
        }
        finally
        {
            await PostgresAdvisoryLockService
                .ReleaseAsync(context, BackgroundWorkerAdvisoryLocks.JobDispatchCoordinator, stoppingToken)
                .ConfigureAwait(false);
        }
    }
}
