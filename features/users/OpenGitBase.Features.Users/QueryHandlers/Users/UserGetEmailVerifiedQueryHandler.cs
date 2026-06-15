using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Queries.Users;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Users.QueryHandlers.Users;

public class UserGetEmailVerifiedQueryHandler : IQueryHandler<UserGetEmailVerifiedQuery, bool>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public UserGetEmailVerifiedQueryHandler(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<bool>> RunQueryAsync(
        UserGetEmailVerifiedQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var credentials = await context
            .Set<UserCredentialsEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                x => x.UserId == query.UserId.Value && !x.Deleted,
                cancellationToken
            );

        if (credentials == null)
        {
            return Option<bool>.None;
        }

        return credentials.EmailVerified;
    }
}
