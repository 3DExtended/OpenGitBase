using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.Entities;
using OpenGitBase.Features.AccountSecurity.Events;
using OpenGitBase.Features.AccountSecurity.SecondFactor;
using OpenGitBase.Features.AccountSecurity.Totp;
using OpenGitBase.Features.Users;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.AccountSecurity.QueryHandlers;

public class ConfirmTotpSetupQueryHandler : IQueryHandler<ConfirmTotpSetupQuery, TotpConfirmation>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IEmailProtectionService _protection;
    private readonly IPasswordHasherService _hasher;
    private readonly ISystemClock _clock;
    private readonly IRemoteIpAddressAccessor _remoteIp;
    private readonly IQueryProcessor _queryProcessor;
    private readonly ILogger<ConfirmTotpSetupQueryHandler> _logger;

    public ConfirmTotpSetupQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IEmailProtectionService protection,
        IPasswordHasherService hasher,
        ISystemClock clock,
        IRemoteIpAddressAccessor remoteIp,
        IQueryProcessor queryProcessor,
        ILogger<ConfirmTotpSetupQueryHandler> logger
    )
    {
        _contextFactory = contextFactory;
        _protection = protection;
        _hasher = hasher;
        _clock = clock;
        _remoteIp = remoteIp;
        _queryProcessor = queryProcessor;
        _logger = logger;
    }

    public async Task<Option<TotpConfirmation>> RunQueryAsync(
        ConfirmTotpSetupQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var credentials = await context
            .Set<UserCredentialsEntity>()
            .FirstOrDefaultAsync(c => c.UserId == query.UserId.Value && !c.Deleted, cancellationToken);
        if (credentials is null)
        {
            return Option<TotpConfirmation>.None;
        }

        var authenticator = await context
            .Set<UserTotpAuthenticatorEntity>()
            .FirstOrDefaultAsync(a => a.UserId == query.UserId.Value, cancellationToken);
        if (authenticator?.PendingSecretProtected is null)
        {
            return new TotpConfirmation(TotpConfirmationStatus.NoPendingSetup);
        }

        var now = _clock.UtcNow;
        var pendingSecret = _protection.DecryptSecret(authenticator.PendingSecretProtected);
        if (!TotpAlgorithm.TryVerify(TotpAlgorithm.DecodeSecret(pendingSecret), query.Code, now, out var step))
        {
            return new TotpConfirmation(TotpConfirmationStatus.InvalidCode);
        }

        authenticator.SecretProtected = authenticator.PendingSecretProtected;
        authenticator.PendingSecretProtected = null;
        authenticator.ConfirmedAt = now;
        // The code just typed must not also sign someone in.
        authenticator.LastUsedTimeStep = step;

        var newlyEnabled = credentials.TwoFactorEnabledAt is null;
        IReadOnlyList<string>? recoveryCodes = null;
        if (newlyEnabled)
        {
            credentials.TwoFactorEnabledAt = now;
            UserSessionRevocation.RevokeSessionsIssuedBefore(credentials, now);
            recoveryCodes = await RecoveryCodes.ReplaceSetAsync(context, _hasher, credentials.UserId, now, cancellationToken);
        }

        await context.SaveChangesAsync(cancellationToken);
        if (newlyEnabled)
        {
            var remoteIp = _remoteIp.RemoteIpAddress;
            SecurityEventLog.Write(_logger, SecurityEvent.TwoFactorEnabled, credentials.UserId, remoteIp, "method=totp");
            await _queryProcessor.NotifyAsync(
                credentials.UserId,
                SecurityNotification.TwoFactorEnabled,
                now,
                remoteIp,
                cancellationToken
            );
        }

        return new TotpConfirmation(TotpConfirmationStatus.Confirmed, newlyEnabled, recoveryCodes);
    }
}
