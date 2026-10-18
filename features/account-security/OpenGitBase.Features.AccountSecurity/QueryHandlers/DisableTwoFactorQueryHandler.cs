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

public class DisableTwoFactorQueryHandler : IQueryHandler<DisableTwoFactorQuery, Unit>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly ISystemClock _clock;
    private readonly ILogger<DisableTwoFactorQueryHandler> _logger;

    public DisableTwoFactorQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        ISystemClock clock,
        ILogger<DisableTwoFactorQueryHandler> logger
    )
    {
        _contextFactory = contextFactory;
        _clock = clock;
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

        var wasEnabled = credentials.TwoFactorEnabledAt is not null;
        credentials.TwoFactorEnabledAt = null;
        SecondFactorAttempts.RecordSuccess(credentials);
        UserSessionRevocation.RevokeSessionsIssuedBefore(credentials, _clock.UtcNow);
        await context.SaveChangesAsync(cancellationToken);
        if (wasEnabled)
        {
            _logger.LogInformation("Two-factor sign-in disabled for user {UserId}", query.UserId.Value);
        }

        return Unit.Value;
    }
}
