using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.Entities;
using OpenGitBase.Features.AccountSecurity.SecondFactor;
using OpenGitBase.Features.Users;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.AccountSecurity.QueryHandlers;

public class DisableTwoFactorQueryHandler : IQueryHandler<DisableTwoFactorQuery, Unit>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly ISystemClock _clock;

    public DisableTwoFactorQueryHandler(IDbContextFactory<OpenGitBaseDbContext> contextFactory, ISystemClock clock)
    {
        _contextFactory = contextFactory;
        _clock = clock;
    }

    public async Task<Option<Unit>> RunQueryAsync(DisableTwoFactorQuery query, CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var credentials = await context
            .Set<UserCredentialsEntity>()
            .FirstOrDefaultAsync(c => c.UserId == query.UserId.Value && !c.Deleted, cancellationToken);
        if (credentials is null)
        {
            return Option<Unit>.None;
        }

        var authenticator = await context
            .Set<UserTotpAuthenticatorEntity>()
            .FirstOrDefaultAsync(a => a.UserId == query.UserId.Value, cancellationToken);
        if (authenticator is not null)
        {
            context.Set<UserTotpAuthenticatorEntity>().Remove(authenticator);
        }

        credentials.TwoFactorEnabledAt = null;
        SecondFactorAttempts.RecordSuccess(credentials);
        UserSessionRevocation.RevokeSessionsIssuedBefore(credentials, _clock.UtcNow);
        await context.SaveChangesAsync(cancellationToken);
        return Unit.Value;
    }
}
