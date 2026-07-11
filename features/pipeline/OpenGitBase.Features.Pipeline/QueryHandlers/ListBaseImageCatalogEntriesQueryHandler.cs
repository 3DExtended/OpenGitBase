using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;

namespace OpenGitBase.Features.Pipeline.QueryHandlers;

public sealed class ListBaseImageCatalogEntriesQueryHandler
    : IQueryHandler<ListBaseImageCatalogEntriesQuery, IReadOnlyList<BaseImageCatalogEntryDto>>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IMapper _mapper;

    public ListBaseImageCatalogEntriesQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IMapper mapper
    )
    {
        _contextFactory = contextFactory;
        _mapper = mapper;
    }

    public async Task<Option<IReadOnlyList<BaseImageCatalogEntryDto>>> RunQueryAsync(
        ListBaseImageCatalogEntriesQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entries = await context
            .Set<BaseImageCatalogEntity>()
            .OrderBy(entity => entity.Slug)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return Option.From(
            (IReadOnlyList<BaseImageCatalogEntryDto>)entries
                .Select(_mapper.Map<BaseImageCatalogEntryDto>)
                .ToList()
        );
    }
}
