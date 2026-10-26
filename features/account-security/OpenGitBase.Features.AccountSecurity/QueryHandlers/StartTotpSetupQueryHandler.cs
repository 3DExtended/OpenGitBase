using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.Entities;
using OpenGitBase.Features.AccountSecurity.Totp;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.AccountSecurity.QueryHandlers;

public class StartTotpSetupQueryHandler : IQueryHandler<StartTotpSetupQuery, TotpSetup>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IEmailProtectionService _protection;
    private readonly ISystemClock _clock;

    public StartTotpSetupQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IEmailProtectionService protection,
        ISystemClock clock
    )
    {
        _contextFactory = contextFactory;
        _protection = protection;
        _clock = clock;
    }

    public async Task<Option<TotpSetup>> RunQueryAsync(StartTotpSetupQuery query, CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var credentials = await context
            .Set<UserCredentialsEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.UserId == query.UserId.Value && !c.Deleted, cancellationToken);
        if (credentials is null)
        {
            return Option<TotpSetup>.None;
        }

        var secret = TotpAlgorithm.GenerateSecret();
        var authenticator = await context
            .Set<UserTotpAuthenticatorEntity>()
            .FirstOrDefaultAsync(a => a.UserId == query.UserId.Value, cancellationToken);
        if (authenticator is null)
        {
            authenticator = new UserTotpAuthenticatorEntity { UserId = query.UserId.Value, CreatedAt = _clock.UtcNow };
            context.Set<UserTotpAuthenticatorEntity>().Add(authenticator);
        }

        // A confirmed secret stays in force until the new app has proven it works.
        authenticator.PendingSecretProtected = _protection.EncryptSecret(secret);
        await context.SaveChangesAsync(cancellationToken);

        return new TotpSetup(TotpAlgorithm.BuildOtpauthUri(query.Issuer, credentials.Username, secret), secret);
    }
}
