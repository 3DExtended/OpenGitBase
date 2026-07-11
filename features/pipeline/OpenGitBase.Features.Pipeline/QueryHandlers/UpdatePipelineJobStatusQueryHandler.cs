using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;

namespace OpenGitBase.Features.Pipeline.QueryHandlers;

public sealed class UpdatePipelineJobStatusQueryHandler
    : IQueryHandler<UpdatePipelineJobStatusQuery, PipelineJobDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IMapper _mapper;
    private readonly AdvancePipelineRunQueryHandler _advancePipelineRunQueryHandler;

    public UpdatePipelineJobStatusQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IMapper mapper,
        AdvancePipelineRunQueryHandler advancePipelineRunQueryHandler
    )
    {
        _contextFactory = contextFactory;
        _mapper = mapper;
        _advancePipelineRunQueryHandler = advancePipelineRunQueryHandler;
    }

    public async Task<Option<PipelineJobDto>> RunQueryAsync(
        UpdatePipelineJobStatusQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var job = await context
            .Set<PipelineJobEntity>()
            .FirstOrDefaultAsync(entity => entity.Id == query.JobId.Value, cancellationToken)
            .ConfigureAwait(false);
        if (job is null)
        {
            return Option<PipelineJobDto>.None;
        }

        var previous = job.Status;
        job.Status = query.Status;
        if (query.Status is PipelineJobStatus.Passed or PipelineJobStatus.Failed or PipelineJobStatus.Cancelled)
        {
            job.FinishedAt = DateTimeOffset.UtcNow;
        }

        context.Set<JobStatusTransitionEntity>()
            .Add(
                new JobStatusTransitionEntity
                {
                    Id = Guid.NewGuid(),
                    JobId = job.Id,
                    FromStatus = previous,
                    ToStatus = query.Status,
                    Message = query.Message,
                    CreatedAt = DateTimeOffset.UtcNow,
                }
            );

        var normalizedLogLines = query
            .LogLines.Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.TrimEnd('\r', '\n'))
            .Where(line => line.Length > 0)
            .ToList();
        if (normalizedLogLines.Count == 0 && !string.IsNullOrWhiteSpace(query.Message))
        {
            normalizedLogLines.Add(query.Message.Trim());
        }

        if (normalizedLogLines.Count > 0)
        {
            var section = string.IsNullOrWhiteSpace(query.LogSection)
                ? "script"
                : query.LogSection.Trim();
            var timestamp = DateTimeOffset.UtcNow;
            foreach (var line in normalizedLogLines)
            {
                context.Set<PipelineJobLogEntity>()
                    .Add(
                        new PipelineJobLogEntity
                        {
                            Id = Guid.NewGuid(),
                            JobId = job.Id,
                            Section = section,
                            Line = line.Length <= 4000 ? line : line[..4000],
                            Timestamp = timestamp,
                        }
                    );
            }
        }

        if (query.Status is PipelineJobStatus.Passed or PipelineJobStatus.Failed or PipelineJobStatus.Cancelled)
        {
            var identity = await context
                .Set<JobIdentityEntity>()
                .FirstOrDefaultAsync(entity => entity.JobId == job.Id, cancellationToken)
                .ConfigureAwait(false);
            if (identity is not null)
            {
                identity.RevokedAt = DateTimeOffset.UtcNow;
            }

            if (job.ClaimedByComputeNodeId.HasValue)
            {
                var node = await context
                    .Set<OpenGitBase.Features.ComputeNode.Entities.ComputeNodeEntity>()
                    .FirstOrDefaultAsync(
                        entity => entity.Id == job.ClaimedByComputeNodeId.Value,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                if (node is not null)
                {
                    node.RunningJobs = Math.Max(0, node.RunningJobs - 1);
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (query.Status is PipelineJobStatus.Passed or PipelineJobStatus.Failed or PipelineJobStatus.Cancelled)
        {
            await _advancePipelineRunQueryHandler
                .RunQueryAsync(
                    new AdvancePipelineRunQuery { RunId = PipelineRunId.From(job.RunId) },
                    cancellationToken
                )
                .ConfigureAwait(false);
        }

        return Option.From(_mapper.Map<PipelineJobDto>(job));
    }
}
