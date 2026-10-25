using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.Events;
using OpenGitBase.Features.AccountSecurity.SecondFactor;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.AccountSecurity.QueryHandlers;

public class RegenerateRecoveryCodesQueryHandler : IQueryHandler<RegenerateRecoveryCodesQuery, RecoveryCodeSet>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IPasswordHasherService _hasher;
    private readonly ISystemClock _clock;
    private readonly IRemoteIpAddressAccessor _remoteIp;
    private readonly ILogger<RegenerateRecoveryCodesQueryHandler> _logger;

    public RegenerateRecoveryCodesQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IPasswordHasherService hasher,
        ISystemClock clock,
        IRemoteIpAddressAccessor remoteIp,
        ILogger<RegenerateRecoveryCodesQueryHandler> logger
    )
    {
        _contextFactory = contextFactory;
        _hasher = hasher;
        _clock = clock;
        _remoteIp = remoteIp;
        _logger = logger;
    }

    public async Task<Option<RecoveryCodeSet>> RunQueryAsync(
        RegenerateRecoveryCodesQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var enabled = await context
            .Set<UserCredentialsEntity>()
            .AnyAsync(
                c => c.UserId == query.UserId.Value && !c.Deleted && c.TwoFactorEnabledAt != null,
                cancellationToken
            );
        if (!enabled)
        {
            return Option<RecoveryCodeSet>.None;
        }

        var codes = await RecoveryCodes.ReplaceSetAsync(
            context,
            _hasher,
            query.UserId.Value,
            _clock.UtcNow,
            cancellationToken
        );
        await context.SaveChangesAsync(cancellationToken);
        SecurityEventLog.Write(_logger, SecurityEvent.RecoveryCodesRegenerated, query.UserId.Value, _remoteIp.RemoteIpAddress);
        return new RecoveryCodeSet(codes);
    }
}
