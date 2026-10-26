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

public class ResetTwoFactorQueryHandler : IQueryHandler<ResetTwoFactorQuery, Unit>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly ISystemClock _clock;
    private readonly IQueryProcessor _queryProcessor;
    private readonly IRemoteIpAddressAccessor _remoteIp;
    private readonly ILogger<ResetTwoFactorQueryHandler> _logger;

    public ResetTwoFactorQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        ISystemClock clock,
        IQueryProcessor queryProcessor,
        IRemoteIpAddressAccessor remoteIp,
        ILogger<ResetTwoFactorQueryHandler> logger
    )
    {
        _contextFactory = contextFactory;
        _clock = clock;
        _queryProcessor = queryProcessor;
        _remoteIp = remoteIp;
        _logger = logger;
    }

    public async Task<Option<Unit>> RunQueryAsync(ResetTwoFactorQuery query, CancellationToken cancellationToken)
    {
        var normalizedUsername = query.Username.Trim().ToLowerInvariant();
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var credentials = await (
            from user in context.Set<UserEntity>()
            where user.NormalizedUsername == normalizedUsername
            join candidate in context.Set<UserCredentialsEntity>() on user.Id equals candidate.UserId
            where !candidate.Deleted
            select candidate
        ).FirstOrDefaultAsync(cancellationToken);
        if (credentials is null)
        {
            return Option<Unit>.None;
        }

        var userId = credentials.UserId;
        context.Set<UserTotpAuthenticatorEntity>().RemoveRange(
            await context.Set<UserTotpAuthenticatorEntity>().Where(a => a.UserId == userId).ToListAsync(cancellationToken)
        );
        context.Set<UserPasskeyEntity>().RemoveRange(
            await context.Set<UserPasskeyEntity>().Where(p => p.UserId == userId).ToListAsync(cancellationToken)
        );
        await RecoveryCodes.RemoveSetAsync(context, userId, cancellationToken);

        var now = _clock.UtcNow;
        credentials.TwoFactorEnabledAt = null;
        SecondFactorAttempts.RecordSuccess(credentials);
        UserSessionRevocation.RevokeSessionsIssuedBefore(credentials, now);
        await context.SaveChangesAsync(cancellationToken);

        SecurityEventLog.Write(
            _logger,
            SecurityEvent.TwoFactorAdminReset,
            userId,
            _remoteIp.RemoteIpAddress,
            $"admin={query.AdminUserId.Value}"
        );
        // The address is the administrator's, which the user has no use for.
        await _queryProcessor.NotifyAsync(userId, SecurityNotification.TwoFactorReset, now, null, cancellationToken);
        return Unit.Value;
    }
}
