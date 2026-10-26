using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.Entities;
using OpenGitBase.Features.AccountSecurity.SecondFactor;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.AccountSecurity.QueryHandlers;

public class GetAccountSecurityQueryHandler : IQueryHandler<GetAccountSecurityQuery, AccountSecurityStatus>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public GetAccountSecurityQueryHandler(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<AccountSecurityStatus>> RunQueryAsync(
        GetAccountSecurityQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var credentials = await context
            .Set<UserCredentialsEntity>()
            .AsNoTracking()
            .Where(c => c.UserId == query.UserId.Value && !c.Deleted)
            .Select(c => new { c.TwoFactorEnabledAt, c.PasswordHash })
            .FirstOrDefaultAsync(cancellationToken);
        if (credentials is null)
        {
            return Option<AccountSecurityStatus>.None;
        }

        var totpConfigured = await context
            .Set<UserTotpAuthenticatorEntity>()
            .AnyAsync(a => a.UserId == query.UserId.Value && a.SecretProtected != null, cancellationToken);
        return new AccountSecurityStatus(
            credentials.TwoFactorEnabledAt is not null,
            totpConfigured,
            credentials.PasswordHash is not null,
            await RecoveryCodes.CountUnusedAsync(context, query.UserId.Value, cancellationToken)
        );
    }
}
