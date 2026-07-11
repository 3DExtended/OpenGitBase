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

    public UpdatePipelineJobStatusQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IMapper mapper
    )
    {
        _contextFactory = contextFactory;
        _mapper = mapper;
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
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Option.From(_mapper.Map<PipelineJobDto>(job));
    }
}
