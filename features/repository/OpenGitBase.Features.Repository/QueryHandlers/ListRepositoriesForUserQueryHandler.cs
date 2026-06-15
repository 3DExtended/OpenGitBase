using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.RepositoryMember.Contracts;

namespace OpenGitBase.Features.Repository.QueryHandlers;

public class ListRepositoriesForUserQueryHandler
    : IQueryHandler<ListRepositoriesForUserQuery, IReadOnlyList<RepositoryDto>>
{
    private readonly IMapper _mapper;
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IQueryProcessor _queryProcessor;

    public ListRepositoriesForUserQueryHandler(
        IMapper mapper,
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IQueryProcessor queryProcessor
    )
    {
        _mapper = mapper;
        _contextFactory = contextFactory;
        _queryProcessor = queryProcessor;
    }

    public async Task<Option<IReadOnlyList<RepositoryDto>>> RunQueryAsync(
        ListRepositoriesForUserQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var userId = query.UserId.Value;

        var ownedIds = await context
            .Set<RepositoryEntity>()
            .AsNoTracking()
            .Where(x => x.OwnerUserId == userId)
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        var memberIdsResult = await _queryProcessor
            .RunQueryAsync(
                new ListRepositoryIdsForUserQuery { UserId = query.UserId },
                cancellationToken
            )
            .ConfigureAwait(false);
        var memberIds = memberIdsResult.IsSome ? memberIdsResult.Get() : [];

        var orgsResult = await _queryProcessor
            .RunQueryAsync(
                new ListUserOrganizationsQuery { UserId = query.UserId },
                cancellationToken
            )
            .ConfigureAwait(false);
        var orgIds = orgsResult.IsSome ? orgsResult.Get().Select(org => org.Id.Value).ToList() : [];

        var orgOwnedIds =
            orgIds.Count == 0
                ? []
                : await context
                    .Set<RepositoryEntity>()
                    .AsNoTracking()
                    .Where(x => orgIds.Contains(x.OwnerUserId))
                    .Select(x => x.Id)
                    .ToListAsync(cancellationToken);

        var allIds = ownedIds.Concat(memberIds).Concat(orgOwnedIds).Distinct().ToList();

        var entities = await context
            .Set<RepositoryEntity>()
            .AsNoTracking()
            .Include(x => x.OwnerUser)
            .Where(x => allIds.Contains(x.Id))
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        return Option.From<IReadOnlyList<RepositoryDto>>(
            entities.Select(entity => _mapper.Map<RepositoryDto>(entity)).ToList()
        );
    }
}
