using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.AccountSecurity.Challenges;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.Entities;
using OpenGitBase.Features.AccountSecurity.Events;
using OpenGitBase.Features.AccountSecurity.Passkeys;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.AccountSecurity.QueryHandlers;

public class ReauthenticateWithPasskeyQueryHandler
    : IQueryHandler<ReauthenticateWithPasskeyQuery, ReauthenticationResult>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly WebAuthnOptions _webAuthnOptions;
    private readonly IPasswordHasherService _passwordHasher;
    private readonly ISystemClock _clock;
    private readonly IRemoteIpAddressAccessor _remoteIp;
    private readonly ILogger<ReauthenticateWithPasskeyQueryHandler> _logger;

    public ReauthenticateWithPasskeyQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        WebAuthnOptions webAuthnOptions,
        IPasswordHasherService passwordHasher,
        ISystemClock clock,
        IRemoteIpAddressAccessor remoteIp,
        ILogger<ReauthenticateWithPasskeyQueryHandler> logger
    )
    {
        _contextFactory = contextFactory;
        _webAuthnOptions = webAuthnOptions;
        _passwordHasher = passwordHasher;
        _clock = clock;
        _remoteIp = remoteIp;
        _logger = logger;
    }

    public async Task<Option<ReauthenticationResult>> RunQueryAsync(
        ReauthenticateWithPasskeyQuery query,
        CancellationToken cancellationToken
    )
    {
        var now = _clock.UtcNow;
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var credentials = await context
            .Set<UserCredentialsEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == query.UserId.Value && !c.Deleted, cancellationToken);
        if (credentials is null)
        {
            return Option<ReauthenticationResult>.None;
        }

        var withPassword = !string.IsNullOrEmpty(query.Password);
        if (
            withPassword
            && (credentials.PasswordHash is null || !_passwordHasher.VerifyPassword(credentials.PasswordHash, query.Password))
        )
        {
            return new ReauthenticationResult(ReauthenticationStatus.InvalidCredentials);
        }

        var assertion = await PasskeyCeremonies.VerifyAssertionAsync(
            context,
            _webAuthnOptions,
            AuthenticationChallengePurpose.PasskeyReauthentication,
            query.UserId.Value,
            query.AssertionResponseJson,
            now,
            _logger,
            _remoteIp.RemoteIpAddress,
            cancellationToken,
            requireUserVerification: !withPassword
        );
        if (assertion.Outcome == PasskeyAssertionOutcome.ChallengeExpired)
        {
            return new ReauthenticationResult(ReauthenticationStatus.ChallengeExpired);
        }

        if (assertion.Outcome == PasskeyAssertionOutcome.Rejected)
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
