using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.Entities;
using OpenGitBase.Features.AccountSecurity.SecondFactor;
using OpenGitBase.Features.Users;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.AccountSecurity.QueryHandlers;

public class EnableTwoFactorWithPasskeysQueryHandler
    : IQueryHandler<EnableTwoFactorWithPasskeysQuery, PasskeyTwoFactorActivation>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IPasswordHasherService _hasher;
    private readonly ISystemClock _clock;
    private readonly ILogger<EnableTwoFactorWithPasskeysQueryHandler> _logger;

    public EnableTwoFactorWithPasskeysQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IPasswordHasherService hasher,
        ISystemClock clock,
        ILogger<EnableTwoFactorWithPasskeysQueryHandler> logger
    )
    {
        _contextFactory = contextFactory;
        _hasher = hasher;
        _clock = clock;
        _logger = logger;
    }

    public async Task<Option<PasskeyTwoFactorActivation>> RunQueryAsync(
        EnableTwoFactorWithPasskeysQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var credentials = await context
            .Set<UserCredentialsEntity>()
            .FirstOrDefaultAsync(c => c.UserId == query.UserId.Value && !c.Deleted, cancellationToken);
        if (credentials is null)
        {
            return Option<PasskeyTwoFactorActivation>.None;
        }

        if (credentials.TwoFactorEnabledAt is not null)
        {
            return new PasskeyTwoFactorActivation(PasskeyTwoFactorActivationStatus.AlreadyEnabled);
        }

        if (!await context.Set<UserPasskeyEntity>().AnyAsync(p => p.UserId == query.UserId.Value, cancellationToken))
        {
            return new PasskeyTwoFactorActivation(PasskeyTwoFactorActivationStatus.NoPasskeys);
        }

        var now = _clock.UtcNow;
        credentials.TwoFactorEnabledAt = now;
        UserSessionRevocation.RevokeSessionsIssuedBefore(credentials, now);
        var recoveryCodes = await RecoveryCodes.ReplaceSetAsync(context, _hasher, credentials.UserId, now, cancellationToken);
        await context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Two-factor sign-in enabled for user {UserId} with passkeys", query.UserId.Value);
        return new PasskeyTwoFactorActivation(PasskeyTwoFactorActivationStatus.Enabled, recoveryCodes);
    }
}
