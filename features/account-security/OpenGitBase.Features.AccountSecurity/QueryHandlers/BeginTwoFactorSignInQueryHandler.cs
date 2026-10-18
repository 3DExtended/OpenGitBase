using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.AccountSecurity.Challenges;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.Entities;
using OpenGitBase.Features.AccountSecurity.SecondFactor;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.AccountSecurity.QueryHandlers;

public class BeginTwoFactorSignInQueryHandler : IQueryHandler<BeginTwoFactorSignInQuery, TwoFactorSignInStart>
{
    private static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(5);

    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly WebAuthnOptions _webAuthnOptions;
    private readonly ISystemClock _clock;

    public BeginTwoFactorSignInQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        WebAuthnOptions webAuthnOptions,
        ISystemClock clock
    )
    {
        _contextFactory = contextFactory;
        _webAuthnOptions = webAuthnOptions;
        _clock = clock;
    }

    public async Task<Option<TwoFactorSignInStart>> RunQueryAsync(
        BeginTwoFactorSignInQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var credentials = await context
            .Set<UserCredentialsEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == query.UserId.Value && !c.Deleted, cancellationToken);
        if (credentials is null)
        {
            return Option<TwoFactorSignInStart>.None;
        }

        if (credentials.TwoFactorEnabledAt is null)
        {
            return TwoFactorSignInStart.NotRequired;
        }

        var methods = new List<string>();
        if (
            await context
                .Set<UserTotpAuthenticatorEntity>()
                .AnyAsync(a => a.UserId == query.UserId.Value && a.SecretProtected != null, cancellationToken)
        )
        {
            methods.Add("totp");
        }

        if (
            _webAuthnOptions.Enabled
            && await context.Set<UserPasskeyEntity>().AnyAsync(p => p.UserId == query.UserId.Value, cancellationToken)
        )
        {
            methods.Add("passkey");
        }

        if (await RecoveryCodes.CountUnusedAsync(context, query.UserId.Value, cancellationToken) > 0)
        {
            methods.Add("recovery");
        }

        var token = AuthenticationChallenges.NewToken();
        await AuthenticationChallenges.CreateAsync(
            context,
            AuthenticationChallengePurpose.TwoFactorSignIn,
            query.UserId.Value,
            token,
            ChallengeLifetime,
            _clock.UtcNow,
            cancellationToken
        );
        return new TwoFactorSignInStart(true, token, methods);
    }
}
