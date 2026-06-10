using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Queries.Users;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Users.QueryHandlers.Users;

public class UserExistsByUsernameQueryHandler : IQueryHandler<UserExistsByUsernameQuery, bool>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public UserExistsByUsernameQueryHandler(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<bool>> RunQueryAsync(
        UserExistsByUsernameQuery query,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(query.Username))
        {
            return Option<bool>.None;
        }

        var normalized = query.Username.Trim().ToLowerInvariant();

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var exists = await context
            .Set<UserEntity>()
            .AsNoTracking()
            .AnyAsync(x => x.NormalizedUsername == normalized, cancellationToken);

        return exists;
    }
}
