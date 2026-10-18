using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.AccountSecurity.Entities;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.AccountSecurity.QueryHandlers;

public class DeletePasskeyQueryHandler : IQueryHandler<DeletePasskeyQuery, PasskeyDeletion>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly ILogger<DeletePasskeyQueryHandler> _logger;

    public DeletePasskeyQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        ILogger<DeletePasskeyQueryHandler> logger
    )
    {
        _contextFactory = contextFactory;
        _logger = logger;
    }

    public async Task<Option<PasskeyDeletion>> RunQueryAsync(DeletePasskeyQuery query, CancellationToken cancellationToken)
    {
        var userId = query.UserId.Value;
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var passkeys = context.Set<UserPasskeyEntity>().Where(passkey => passkey.UserId == userId);
        if (!await passkeys.AnyAsync(passkey => passkey.Id == query.PasskeyId, cancellationToken))
        {
            return Option<PasskeyDeletion>.None;
        }

        if (await IsLastSecondFactorAsync(context, userId, query.PasskeyId, cancellationToken))
        {
            return new PasskeyDeletion(PasskeyDeletionStatus.LastSecondFactor);
        }

        var deleted = await passkeys.Where(passkey => passkey.Id == query.PasskeyId).ExecuteDeleteAsync(cancellationToken);
        if (deleted == 0)
        {
            return Option<PasskeyDeletion>.None;
        }

        _logger.LogInformation("Passkey {PasskeyId} removed for user {UserId}", query.PasskeyId, userId);
        return new PasskeyDeletion(PasskeyDeletionStatus.Deleted);
    }

    /// <summary>With 2FA on, someone signing in needs an authenticator app or another passkey after this one is gone.</summary>
    private static async Task<bool> IsLastSecondFactorAsync(
        OpenGitBaseDbContext context,
        Guid userId,
        Guid passkeyId,
        CancellationToken cancellationToken
    )
    {
        var twoFactorOn = await context
            .Set<UserCredentialsEntity>()
            .AnyAsync(c => c.UserId == userId && c.TwoFactorEnabledAt != null, cancellationToken);
        if (!twoFactorOn)
        {
            return false;
        }

        var hasAuthenticator = await context
            .Set<UserTotpAuthenticatorEntity>()
            .AnyAsync(a => a.UserId == userId && a.SecretProtected != null, cancellationToken);
        var hasOtherPasskey = await context
            .Set<UserPasskeyEntity>()
            .AnyAsync(p => p.UserId == userId && p.Id != passkeyId, cancellationToken);
        return !hasAuthenticator && !hasOtherPasskey;
    }
}
