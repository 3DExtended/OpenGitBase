using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Features.Repository.QueryHandlers;

public class ListRecentPublicRepositoriesQueryHandler
    : IQueryHandler<ListRecentPublicRepositoriesQuery, IReadOnlyList<RepositoryDto>>
{
    private readonly IMapper _mapper;
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public ListRecentPublicRepositoriesQueryHandler(
        IMapper mapper,
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _mapper = mapper;
        _contextFactory = contextFactory;
    }

    public async Task<Option<IReadOnlyList<RepositoryDto>>> RunQueryAsync(
        ListRecentPublicRepositoriesQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var entities = await context
            .Set<RepositoryEntity>()
            .AsNoTracking()
            .Where(x => !x.IsPrivate)
            .OrderByDescending(x => x.Id)
            .Take(query.Limit)
            .ToListAsync(cancellationToken);

        var dtos = entities.Select(entity => _mapper.Map<RepositoryDto>(entity)).ToList();
        await RepositoryOwnerMetadataEnricher
            .EnrichAsync(dtos, context, cancellationToken)
            .ConfigureAwait(false);

        return Option.From<IReadOnlyList<RepositoryDto>>(dtos);
    }
}
