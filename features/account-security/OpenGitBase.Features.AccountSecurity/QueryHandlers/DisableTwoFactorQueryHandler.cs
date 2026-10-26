using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.Entities;
using OpenGitBase.Features.AccountSecurity.Events;
using OpenGitBase.Features.AccountSecurity.SecondFactor;
using OpenGitBase.Features.Users;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.AccountSecurity.QueryHandlers;

public class DisableTwoFactorQueryHandler : IQueryHandler<DisableTwoFactorQuery, Unit>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly ISystemClock _clock;
    private readonly IRemoteIpAddressAccessor _remoteIp;
    private readonly IQueryProcessor _queryProcessor;
    private readonly ILogger<DisableTwoFactorQueryHandler> _logger;

    public DisableTwoFactorQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        ISystemClock clock,
        IRemoteIpAddressAccessor remoteIp,
        IQueryProcessor queryProcessor,
        ILogger<DisableTwoFactorQueryHandler> logger
    )
    {
        _contextFactory = contextFactory;
        _clock = clock;
        _remoteIp = remoteIp;
        _queryProcessor = queryProcessor;
        _logger = logger;
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

        await RecoveryCodes.RemoveSetAsync(context, query.UserId.Value, cancellationToken);

        var wasEnabled = credentials.TwoFactorEnabledAt is not null;
        credentials.TwoFactorEnabledAt = null;
        SecondFactorAttempts.RecordSuccess(credentials);
        var now = _clock.UtcNow;
        UserSessionRevocation.RevokeSessionsIssuedBefore(credentials, now);
        await context.SaveChangesAsync(cancellationToken);
        if (wasEnabled)
        {
            var remoteIp = _remoteIp.RemoteIpAddress;
            SecurityEventLog.Write(_logger, SecurityEvent.TwoFactorDisabled, credentials.UserId, remoteIp);
            await _queryProcessor.NotifyAsync(
                credentials.UserId,
                SecurityNotification.TwoFactorDisabled,
                now,
                remoteIp,
                cancellationToken
            );
        }

        return Unit.Value;
    }
}
