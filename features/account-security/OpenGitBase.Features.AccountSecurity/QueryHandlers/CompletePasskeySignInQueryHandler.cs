using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.Entities;
using OpenGitBase.Features.AccountSecurity.Events;
using OpenGitBase.Features.AccountSecurity.Passkeys;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.AccountSecurity.QueryHandlers;

public class CompletePasskeySignInQueryHandler : IQueryHandler<CompletePasskeySignInQuery, PasskeySignInResult>
{
    private static readonly PasskeySignInResult Rejected = new(PasskeySignInStatus.Rejected);

    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly WebAuthnOptions _webAuthnOptions;
    private readonly ISystemClock _clock;
    private readonly IRemoteIpAddressAccessor _remoteIp;
    private readonly ILogger<CompletePasskeySignInQueryHandler> _logger;

    public CompletePasskeySignInQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        WebAuthnOptions webAuthnOptions,
        ISystemClock clock,
        IRemoteIpAddressAccessor remoteIp,
        ILogger<CompletePasskeySignInQueryHandler> logger
    )
    {
        _contextFactory = contextFactory;
        _webAuthnOptions = webAuthnOptions;
        _clock = clock;
        _remoteIp = remoteIp;
        _logger = logger;
    }

    public async Task<Option<PasskeySignInResult>> RunQueryAsync(
        CompletePasskeySignInQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var remoteIp = _remoteIp.RemoteIpAddress;
        var assertion = await PasskeyCeremonies.VerifyAssertionAsync(
            context,
            _webAuthnOptions,
            AuthenticationChallengePurpose.PasskeySignIn,
            null,
            query.AssertionResponseJson,
            _clock.UtcNow,
            _logger,
            remoteIp,
            cancellationToken
        );
        if (assertion.Outcome != PasskeyAssertionOutcome.Verified || assertion.UserId is not { } userId)
        {
            return Rejected;
        }

        if (
            !await context
                .Set<UserCredentialsEntity>()
                .AnyAsync(c => c.UserId == userId && !c.Deleted, cancellationToken)
        )
        {
            SecurityEventLog.Write(_logger, SecurityEvent.PasskeyRejected, userId, remoteIp, "reason=account_deleted");
            return Rejected;
        }

        SecurityEventLog.Write(_logger, SecurityEvent.PasskeySignedIn, userId, remoteIp);
        return new PasskeySignInResult(PasskeySignInStatus.SignedIn, UserId.From(userId));
    }
}
