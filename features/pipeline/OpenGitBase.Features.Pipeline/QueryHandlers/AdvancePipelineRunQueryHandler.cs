using System.Text.Json;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;
using OpenGitBase.Features.Pipeline.Services;

namespace OpenGitBase.Features.Pipeline.QueryHandlers;

public sealed class AdvancePipelineRunQueryHandler : IQueryHandler<AdvancePipelineRunQuery, PipelineRunDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IMapper _mapper;
    private readonly IJobAvailableEventPublisher _publisher;

    public AdvancePipelineRunQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IMapper mapper,
        IJobAvailableEventPublisher publisher
    )
    {
        _contextFactory = contextFactory;
        _mapper = mapper;
        _publisher = publisher;
    }

    public async Task<Option<PipelineRunDto>> RunQueryAsync(
        AdvancePipelineRunQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var run = await context
            .Set<PipelineRunEntity>()
            .FirstOrDefaultAsync(entity => entity.Id == query.RunId.Value, cancellationToken)
            .ConfigureAwait(false);
        if (run is null)
        {
            return Option<PipelineRunDto>.None;
        }

        var jobs = await context
            .Set<PipelineJobEntity>()
            .Where(entity => entity.RunId == run.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var stageOrder = JsonSerializer.Deserialize<List<string>>(run.StageOrderJson) ?? [];
        var stageMap = stageOrder
            .Select((name, index) => new { name, index })
            .ToDictionary(pair => pair.name, pair => pair.index, StringComparer.Ordinal);

        var stageGroups = jobs
            .GroupBy(job => job.Stage)
            .OrderBy(group => stageMap.TryGetValue(group.Key, out var index) ? index : int.MaxValue)
            .ToList();

        var hasFailure = false;
        foreach (var stage in stageGroups)
        {
            var stageJobs = stage.ToList();
            var stageHasBlockingFailure = stageJobs.Any(job =>
                job.Status is PipelineJobStatus.Failed or PipelineJobStatus.Cancelled
            );
            var stageInFlight = stageJobs.Any(job =>
                job.Status is PipelineJobStatus.Queued or PipelineJobStatus.Running
            );

            if (hasFailure)
            {
                foreach (var blockedJob in stageJobs.Where(job => job.Status == PipelineJobStatus.Blocked))
                {
                    blockedJob.Status = PipelineJobStatus.Cancelled;
                    blockedJob.FinishedAt = DateTimeOffset.UtcNow;
                    context.Set<JobStatusTransitionEntity>()
                        .Add(
                            new JobStatusTransitionEntity
                            {
                                Id = Guid.NewGuid(),
                                JobId = blockedJob.Id,
                                FromStatus = PipelineJobStatus.Blocked,
                                ToStatus = PipelineJobStatus.Cancelled,
                                Message = "Skipped because earlier stage failed.",
                                CreatedAt = DateTimeOffset.UtcNow,
                            }
                        );
                }

                continue;
            }

            if (stageHasBlockingFailure)
            {
                hasFailure = true;
                continue;
            }

            if (stageInFlight)
            {
                // Current active stage still running; keep later stages blocked.
                break;
            }

            foreach (var blockedJob in stageJobs.Where(job => job.Status == PipelineJobStatus.Blocked))
            {
                blockedJob.Status = PipelineJobStatus.Queued;
                context.Set<JobStatusTransitionEntity>()
                    .Add(
                        new JobStatusTransitionEntity
                        {
                            Id = Guid.NewGuid(),
                            JobId = blockedJob.Id,
                            FromStatus = PipelineJobStatus.Blocked,
                            ToStatus = PipelineJobStatus.Queued,
                            Message = "Stage activated.",
                            CreatedAt = DateTimeOffset.UtcNow,
                        }
                    );
                await _publisher.PublishAsync(blockedJob.Id, cancellationToken).ConfigureAwait(false);
            }
        }

        run.Status = ResolveRunStatus(jobs);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Option.From(_mapper.Map<PipelineRunDto>(run));
    }

    private static PipelineRunStatus ResolveRunStatus(IReadOnlyCollection<PipelineJobEntity> jobs)
    {
        if (jobs.Any(job => job.Status == PipelineJobStatus.Running))
        {
            return PipelineRunStatus.Running;
        }

        if (jobs.Any(job => job.Status is PipelineJobStatus.Queued or PipelineJobStatus.Blocked))
        {
            return PipelineRunStatus.Queued;
        }

        if (jobs.Any(job => job.Status is PipelineJobStatus.Failed or PipelineJobStatus.Cancelled))
        {
            return PipelineRunStatus.Failed;
        }

        if (jobs.All(job => job.Status == PipelineJobStatus.Passed))
        {
            return PipelineRunStatus.Passed;
        }

        return PipelineRunStatus.Queued;
    }
}
