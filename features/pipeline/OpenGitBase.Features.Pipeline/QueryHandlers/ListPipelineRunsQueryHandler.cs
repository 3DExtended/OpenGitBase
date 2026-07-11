using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;

namespace OpenGitBase.Features.Pipeline.QueryHandlers;

public sealed class ListPipelineRunsQueryHandler
    : IQueryHandler<ListPipelineRunsQuery, IReadOnlyList<PipelineRunDto>>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IMapper _mapper;

    public ListPipelineRunsQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IMapper mapper
    )
    {
        _contextFactory = contextFactory;
        _mapper = mapper;
    }

    public async Task<Option<IReadOnlyList<PipelineRunDto>>> RunQueryAsync(
        ListPipelineRunsQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var runs = await context
            .Set<PipelineRunEntity>()
            .Where(entity => entity.RepositoryId == query.RepositoryId)
            .OrderByDescending(entity => entity.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return Option.From((IReadOnlyList<PipelineRunDto>)runs.Select(_mapper.Map<PipelineRunDto>).ToList());
    }
}
