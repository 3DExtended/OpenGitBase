using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Features.Repository.QueryHandlers;

public class ListUserOwnedRepositoriesQueryHandler
    : IQueryHandler<ListUserOwnedRepositoriesQuery, IReadOnlyList<RepositorySummaryDto>>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public ListUserOwnedRepositoriesQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<IReadOnlyList<RepositorySummaryDto>>> RunQueryAsync(
        ListUserOwnedRepositoriesQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var repos = await context
            .Set<RepositoryEntity>()
            .AsNoTracking()
            .Where(x => x.OwnerUserId == query.UserId.Value)
            .Select(x => new RepositorySummaryDto
            {
                Name = x.Name,
                Slug = x.Slug,
                StorageBytesUsed = x.StorageBytesUsed,
            })
            .ToListAsync(cancellationToken);

        return Option.From<IReadOnlyList<RepositorySummaryDto>>(repos);
    }
}
