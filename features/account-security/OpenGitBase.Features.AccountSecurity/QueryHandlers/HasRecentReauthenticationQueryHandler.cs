using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.AccountSecurity.Challenges;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.Entities;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.AccountSecurity.QueryHandlers;

public class HasRecentReauthenticationQueryHandler : IQueryHandler<HasRecentReauthenticationQuery, bool>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly ISystemClock _clock;

    public HasRecentReauthenticationQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        ISystemClock clock
    )
    {
        _contextFactory = contextFactory;
        _clock = clock;
    }

    public async Task<Option<bool>> RunQueryAsync(
        HasRecentReauthenticationQuery query,
        CancellationToken cancellationToken
    )
    {
        var now = _clock.UtcNow;
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var challenge = await AuthenticationChallenges.FindActiveAsync(
            context,
            AuthenticationChallengePurpose.ReAuthentication,
            Reauthentications.Binding(query.UserId.Value, query.SessionId),
            now,
            cancellationToken
        );
        if (challenge is not null && challenge.UserId == query.UserId.Value)
        {
            return true;
        }

        // Without a password there is nothing to type, so a sign-in moments ago is the confirmation.
        var passwordless = await context
            .Set<UserCredentialsEntity>()
            .AnyAsync(c => c.UserId == query.UserId.Value && !c.Deleted && c.PasswordHash == null, cancellationToken);
        return passwordless && now - query.SessionIssuedAt < Reauthentications.Lifetime;
    }
}
