using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Features.AccountSecurity.Entities;
using OpenGitBase.Features.AccountSecurity.Totp;

namespace OpenGitBase.Features.AccountSecurity.SecondFactor;

internal static class TotpCodes
{
    /// <summary>
    /// Checks the code against the user's confirmed authenticator and spends its time step. The step
    /// is advanced with a conditional update, so a code is accepted once even under concurrent use.
    /// </summary>
    public static async Task<bool> TryUseAsync(
        OpenGitBaseDbContext context,
        IEmailProtectionService protection,
        Guid userId,
        string code,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        var authenticator = await context
            .Set<UserTotpAuthenticatorEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.UserId == userId, cancellationToken);
        if (authenticator?.SecretProtected is null)
        {
            return false;
        }

        var secret = TotpAlgorithm.DecodeSecret(protection.DecryptSecret(authenticator.SecretProtected));
        if (!TotpAlgorithm.TryVerify(secret, code, now, out var step) || step <= authenticator.LastUsedTimeStep)
        {
            return false;
        }

        return await context
            .Set<UserTotpAuthenticatorEntity>()
            .Where(a => a.UserId == userId && a.LastUsedTimeStep < step)
            .ExecuteUpdateAsync(setters => setters.SetProperty(a => a.LastUsedTimeStep, step), cancellationToken) == 1;
    }
}
