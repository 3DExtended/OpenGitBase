using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Users.QueryHandlers.Users;

public class UserGetByInternalIdQueryHandler : IQueryHandler<UserGetByInternalIdQuery, UserId>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public UserGetByInternalIdQueryHandler(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<UserId>> RunQueryAsync(
        UserGetByInternalIdQuery query,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(query.InternalId))
        {
            return Option<UserId>.None;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var credentials = await context
            .Set<UserCredentialsEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.InternalId == query.InternalId && !x.Deleted,
                cancellationToken
            );

        if (credentials == null)
        {
            return Option<UserId>.None;
        }

        return UserId.From(credentials.UserId);
    }
}
