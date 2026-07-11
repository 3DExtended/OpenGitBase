using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;

namespace OpenGitBase.Features.Pipeline.QueryHandlers;

public sealed class GetPipelineRunQueryHandler : IQueryHandler<GetPipelineRunQuery, PipelineRunDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IMapper _mapper;

    public GetPipelineRunQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IMapper mapper
    )
    {
        _contextFactory = contextFactory;
        _mapper = mapper;
    }

    public async Task<Option<PipelineRunDto>> RunQueryAsync(
        GetPipelineRunQuery query,
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
            .OrderBy(entity => entity.CreatedAt)
            .Select(entity => _mapper.Map<PipelineJobDto>(entity))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var runDto = _mapper.Map<PipelineRunDto>(run);
        runDto.Jobs = jobs;
        return Option.From(runDto);
    }
}
