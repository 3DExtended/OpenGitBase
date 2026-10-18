using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.AccountSecurity.Challenges;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.Entities;
using OpenGitBase.Features.AccountSecurity.Passkeys;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.AccountSecurity.QueryHandlers;

public class ReauthenticateWithPasskeyQueryHandler
    : IQueryHandler<ReauthenticateWithPasskeyQuery, ReauthenticationResult>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly WebAuthnOptions _webAuthnOptions;
    private readonly ISystemClock _clock;
    private readonly ILogger<ReauthenticateWithPasskeyQueryHandler> _logger;

    public ReauthenticateWithPasskeyQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        WebAuthnOptions webAuthnOptions,
        ISystemClock clock,
        ILogger<ReauthenticateWithPasskeyQueryHandler> logger
    )
    {
        _contextFactory = contextFactory;
        _webAuthnOptions = webAuthnOptions;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Option<ReauthenticationResult>> RunQueryAsync(
        ReauthenticateWithPasskeyQuery query,
        CancellationToken cancellationToken
    )
    {
        var now = _clock.UtcNow;
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        if (
            !await context
                .Set<UserCredentialsEntity>()
                .AnyAsync(c => c.UserId == query.UserId.Value && !c.Deleted, cancellationToken)
        )
        {
            return Option<ReauthenticationResult>.None;
        }

        var outcome = await PasskeyCeremonies.VerifyAssertionAsync(
            context,
            _webAuthnOptions,
            AuthenticationChallengePurpose.PasskeyReauthentication,
            query.UserId.Value,
            query.AssertionResponseJson,
            now,
            _logger,
            cancellationToken
        );
        if (outcome == PasskeyAssertionOutcome.ChallengeExpired)
        {
            return new ReauthenticationResult(ReauthenticationStatus.ChallengeExpired);
        }

        if (outcome == PasskeyAssertionOutcome.Rejected)
        {
            return new ReauthenticationResult(ReauthenticationStatus.PasskeyRejected);
        }

        await AuthenticationChallenges.CreateAsync(
            context,
            AuthenticationChallengePurpose.ReAuthentication,
            query.UserId.Value,
            Reauthentications.Binding(query.UserId.Value, query.SessionId),
            Reauthentications.Lifetime,
            now,
            cancellationToken
        );
        return new ReauthenticationResult(ReauthenticationStatus.Succeeded);
    }
}
