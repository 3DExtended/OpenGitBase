using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;
using OpenGitBase.Features.Pipeline.Services;

namespace OpenGitBase.Api.Services;

public sealed class JobDispatchCoordinator : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IJobAvailableEventPublisher _publisher;

    public JobDispatchCoordinator(IServiceProvider serviceProvider, IJobAvailableEventPublisher publisher)
    {
        _serviceProvider = serviceProvider;
        _publisher = publisher;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var contextFactory = scope.ServiceProvider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            await using var context = await contextFactory
                .CreateDbContextAsync(stoppingToken)
                .ConfigureAwait(false);
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

            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);
        }
    }
}
