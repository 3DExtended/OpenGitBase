using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.RepositoryMember.Entities;

namespace OpenGitBase.Features.RepositoryMember.QueryHandlers;

public class ListRepositoryIdsForUserQueryHandler
    : IQueryHandler<ListRepositoryIdsForUserQuery, IReadOnlyList<Guid>>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public ListRepositoryIdsForUserQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<IReadOnlyList<Guid>>> RunQueryAsync(
        ListRepositoryIdsForUserQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var ids = await context
            .Set<RepositoryMemberEntity>()
            .AsNoTracking()
            .Where(x => x.UserId == query.UserId.Value)
            .Select(x => x.RepositoryId)
            .ToListAsync(cancellationToken);

        return Option.From<IReadOnlyList<Guid>>(ids);
    }
}
