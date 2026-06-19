using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Features.Repository.QueryHandlers;

public class ListPublicRepositoriesQueryHandler
    : IQueryHandler<ListPublicRepositoriesQuery, IReadOnlyList<RepositoryDto>>
{
    private readonly IMapper _mapper;
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public ListPublicRepositoriesQueryHandler(
        IMapper mapper,
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _mapper = mapper;
        _contextFactory = contextFactory;
    }

    public async Task<Option<IReadOnlyList<RepositoryDto>>> RunQueryAsync(
        ListPublicRepositoriesQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var databaseQuery = context
            .Set<RepositoryEntity>()
            .AsNoTracking()
            .Where(x => !x.IsPrivate);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLowerInvariant();
            databaseQuery = databaseQuery.Where(x =>
                x.Name.ToLower().Contains(term) || x.Slug.ToLower().Contains(term)
            );
        }

        var entities = await databaseQuery
            .OrderByDescending(x => x.Name)
            .Take(100)
            .ToListAsync(cancellationToken);

        var dtos = entities.Select(entity => _mapper.Map<RepositoryDto>(entity)).ToList();
        await RepositoryOwnerMetadataEnricher
            .EnrichAsync(dtos, context, cancellationToken)
            .ConfigureAwait(false);

        return Option.From<IReadOnlyList<RepositoryDto>>(dtos);
    }
}
