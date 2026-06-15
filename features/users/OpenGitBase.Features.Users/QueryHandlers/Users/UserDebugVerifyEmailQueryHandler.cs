using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Contracts.Queries.Users;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Users.QueryHandlers.Users;

public class UserDebugVerifyEmailQueryHandler : IQueryHandler<UserDebugVerifyEmailQuery, Unit>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public UserDebugVerifyEmailQueryHandler(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<Unit>> RunQueryAsync(
        UserDebugVerifyEmailQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var credentials = await context
            .Set<UserCredentialsEntity>()
            .FirstOrDefaultAsync(
                x => x.UserId == query.UserId.Value && !x.Deleted,
                cancellationToken
            );

        if (
            credentials == null
            || credentials.SignInProvider
            || credentials.EmailVerified
            || string.IsNullOrEmpty(credentials.EmailCiphertext)
        )
        {
            return Option<Unit>.None;
        }

        credentials.EmailVerified = true;
        credentials.EmailVerificationTokenHash = null;
        credentials.EmailVerificationTokenExpireDate = null;
        await context.SaveChangesAsync(cancellationToken);

        return Unit.Value;
    }
}
