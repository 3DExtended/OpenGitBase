using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;

namespace OpenGitBase.Features.Pipeline.QueryHandlers;

public sealed class GetPipelineJobLogsQueryHandler
    : IQueryHandler<GetPipelineJobLogsQuery, IReadOnlyList<PipelineJobLogDto>>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IMapper _mapper;

    public GetPipelineJobLogsQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IMapper mapper
    )
    {
        _contextFactory = contextFactory;
        _mapper = mapper;
    }

    public async Task<Option<IReadOnlyList<PipelineJobLogDto>>> RunQueryAsync(
        GetPipelineJobLogsQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var jobExists = await context
            .Set<PipelineJobEntity>()
            .AnyAsync(entity => entity.Id == query.JobId.Value, cancellationToken)
            .ConfigureAwait(false);
        if (!jobExists)
        {
            return Option<IReadOnlyList<PipelineJobLogDto>>.None;
        }

        var logs = await context
            .Set<PipelineJobLogEntity>()
            .Where(entity => entity.JobId == query.JobId.Value)
            .OrderBy(entity => entity.Timestamp)
            .Select(entity => _mapper.Map<PipelineJobLogDto>(entity))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return Option.From((IReadOnlyList<PipelineJobLogDto>)logs);
    }
}
