using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.AccountSecurity.Challenges;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.Entities;
using OpenGitBase.Features.AccountSecurity.SecondFactor;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.AccountSecurity.QueryHandlers;

public class ReauthenticateQueryHandler : IQueryHandler<ReauthenticateQuery, ReauthenticationResult>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly IEmailProtectionService _protection;
    private readonly ISystemClock _clock;

    public ReauthenticateQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IPasswordHasherService passwordHasher,
        IEmailProtectionService protection,
        ISystemClock clock
    )
    {
        _contextFactory = contextFactory;
        _passwordHasher = passwordHasher;
        _protection = protection;
        _clock = clock;
    }

    public async Task<Option<ReauthenticationResult>> RunQueryAsync(
        ReauthenticateQuery query,
        CancellationToken cancellationToken
    )
    {
        var now = _clock.UtcNow;
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var credentials = await context
            .Set<UserCredentialsEntity>()
            .FirstOrDefaultAsync(c => c.UserId == query.UserId.Value && !c.Deleted, cancellationToken);
        if (credentials is null)
        {
            return Option<ReauthenticationResult>.None;
        }

        var twoFactorOn = credentials.TwoFactorEnabledAt is not null;
        if (credentials.PasswordHash is null)
        {
            // Google/Apple-only accounts have no password; with 2FA on, the code alone confirms them.
            if (!twoFactorOn)
            {
                return new ReauthenticationResult(ReauthenticationStatus.Unavailable);
            }
        }
        else if (!_passwordHasher.VerifyPassword(credentials.PasswordHash, query.Password))
        {
            return new ReauthenticationResult(ReauthenticationStatus.InvalidCredentials);
        }

        if (twoFactorOn)
        {
            if (SecondFactorAttempts.IsLockedOut(credentials, now))
            {
                return new ReauthenticationResult(ReauthenticationStatus.LockedOut, credentials.SecondFactorLockedUntil);
            }

            if (string.IsNullOrEmpty(query.Code))
            {
                return new ReauthenticationResult(ReauthenticationStatus.SecondFactorRequired);
            }

            if (!await TotpCodes.TryUseAsync(context, _protection, credentials.UserId, query.Code, now, cancellationToken))
            {
                var lockStarted = SecondFactorAttempts.RecordFailure(credentials, now);
                await context.SaveChangesAsync(cancellationToken);
                return lockStarted
                    ? new ReauthenticationResult(ReauthenticationStatus.LockedOut, credentials.SecondFactorLockedUntil)
                    : new ReauthenticationResult(ReauthenticationStatus.InvalidCode);
            }

            SecondFactorAttempts.RecordSuccess(credentials);
            await context.SaveChangesAsync(cancellationToken);
        }

        await AuthenticationChallenges.CreateAsync(
            context,
            AuthenticationChallengePurpose.ReAuthentication,
            credentials.UserId,
            Reauthentications.Binding(credentials.UserId, query.SessionId),
            Reauthentications.Lifetime,
            now,
            cancellationToken
        );
        return new ReauthenticationResult(ReauthenticationStatus.Succeeded);
    }
}
