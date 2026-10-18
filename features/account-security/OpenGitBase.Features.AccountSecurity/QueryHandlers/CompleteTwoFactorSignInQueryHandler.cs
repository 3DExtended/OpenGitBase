using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.AccountSecurity.Challenges;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.Entities;
using OpenGitBase.Features.AccountSecurity.SecondFactor;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.AccountSecurity.QueryHandlers;

public class CompleteTwoFactorSignInQueryHandler : IQueryHandler<CompleteTwoFactorSignInQuery, TwoFactorSignInResult>
{
    private static readonly TwoFactorSignInResult Expired = new(TwoFactorSignInStatus.ChallengeExpired);

    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IEmailProtectionService _protection;
    private readonly ISystemClock _clock;
    private readonly ILogger<CompleteTwoFactorSignInQueryHandler> _logger;

    public CompleteTwoFactorSignInQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IEmailProtectionService protection,
        ISystemClock clock,
        ILogger<CompleteTwoFactorSignInQueryHandler> logger
    )
    {
        _contextFactory = contextFactory;
        _protection = protection;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Option<TwoFactorSignInResult>> RunQueryAsync(
        CompleteTwoFactorSignInQuery query,
        CancellationToken cancellationToken
    )
    {
        var now = _clock.UtcNow;
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var challenge = await AuthenticationChallenges.FindActiveAsync(
            context,
            AuthenticationChallengePurpose.TwoFactorSignIn,
            query.ChallengeToken,
            now,
            cancellationToken
        );
        if (challenge is null)
        {
            return Expired;
        }

        var credentials = await context
            .Set<UserCredentialsEntity>()
            .FirstOrDefaultAsync(c => c.UserId == challenge.UserId && !c.Deleted, cancellationToken);
        if (credentials is null)
        {
            return Expired;
        }

        if (SecondFactorAttempts.IsLockedOut(credentials, now))
        {
            return new TwoFactorSignInResult(TwoFactorSignInStatus.LockedOut, LockedUntil: credentials.SecondFactorLockedUntil);
        }

        if (await TotpCodes.TryUseAsync(context, _protection, credentials.UserId, query.Code, now, cancellationToken))
        {
            if (!await AuthenticationChallenges.TryConsumeAsync(context, challenge.Id, cancellationToken))
            {
                return Expired;
            }

            SecondFactorAttempts.RecordSuccess(credentials);
            await context.SaveChangesAsync(cancellationToken);
            return new TwoFactorSignInResult(TwoFactorSignInStatus.Succeeded, UserId.From(credentials.UserId));
        }

        if (SecondFactorAttempts.RecordFailure(credentials, now))
        {
            await AuthenticationChallenges.TryConsumeAsync(context, challenge.Id, cancellationToken);
            await context.SaveChangesAsync(cancellationToken);
            SecondFactorAttempts.LogLockout(_logger, credentials, "sign-in");
            return new TwoFactorSignInResult(TwoFactorSignInStatus.LockedOut, LockedUntil: credentials.SecondFactorLockedUntil);
        }

        await context.SaveChangesAsync(cancellationToken);
        return new TwoFactorSignInResult(TwoFactorSignInStatus.InvalidCode);
    }
}
