using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;
using OpenGitBase.Features.Pipeline.QueryHandlers;

namespace OpenGitBase.Api.Services;

public sealed class JobTimeoutEnforcerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;

    public JobTimeoutEnforcerService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await using var scope = _serviceProvider.CreateAsyncScope();
            var contextFactory = scope.ServiceProvider.GetRequiredService<
                IDbContextFactory<OpenGitBaseDbContext>
            >();
            var advanceHandler = scope.ServiceProvider.GetRequiredService<AdvancePipelineRunQueryHandler>();
            await using var context = await contextFactory
                .CreateDbContextAsync(stoppingToken)
                .ConfigureAwait(false);
            var now = DateTimeOffset.UtcNow;
            var runningJobs = await context
                .Set<PipelineJobEntity>()
                .Where(entity =>
                    entity.Status == PipelineJobStatus.Running
                    && entity.StartedAt.HasValue
                    && entity.TimeoutSeconds > 0
                )
                .ToListAsync(stoppingToken)
                .ConfigureAwait(false);

            var affectedRunIds = new HashSet<Guid>();
            foreach (var job in runningJobs)
            {
                var timeoutAt = job.StartedAt!.Value.AddSeconds(job.TimeoutSeconds);
                if (timeoutAt > now)
                {
                    continue;
                }

                job.Status = PipelineJobStatus.Failed;
                job.FinishedAt = now;
                context.Set<JobStatusTransitionEntity>()
                    .Add(
                        new JobStatusTransitionEntity
                        {
                            Id = Guid.NewGuid(),
                            JobId = job.Id,
                            FromStatus = PipelineJobStatus.Running,
                            ToStatus = PipelineJobStatus.Failed,
                            Message = "Job timed out.",
                            CreatedAt = now,
                        }
                    );
                var identity = await context
                    .Set<JobIdentityEntity>()
                    .FirstOrDefaultAsync(entity => entity.JobId == job.Id, stoppingToken)
                    .ConfigureAwait(false);
                if (identity is not null)
                {
                    identity.RevokedAt = now;
                }

                affectedRunIds.Add(job.RunId);
            }

            await context.SaveChangesAsync(stoppingToken).ConfigureAwait(false);
            foreach (var runId in affectedRunIds)
            {
                await advanceHandler
                    .RunQueryAsync(
                        new AdvancePipelineRunQuery { RunId = PipelineRunId.From(runId) },
                        stoppingToken
                    )
                    .ConfigureAwait(false);
            }

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken).ConfigureAwait(false);
        }
    }
}
