using System.Globalization;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Auth;
using OpenGitBase.Common.Data;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Api.Services;

/// <summary>
/// Rejects sign-in tokens whose user no longer exists, was deleted, or revoked its sessions
/// (password reset or change) after the token was issued.
/// </summary>
public sealed class UserSessionValidator
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public UserSessionValidator(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<bool> IsActiveAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal.FindFirstValue("identityproviderid"), out var userId))
        {
            return false;
        }

        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        var session = await (
            from user in context.Set<UserEntity>().AsNoTracking()
            where user.Id == userId
            join credentials in context.Set<UserCredentialsEntity>().AsNoTracking()
                on user.Id equals credentials.UserId
                into userCredentials
            from credentials in userCredentials.DefaultIfEmpty()
            select new
            {
                Deleted = credentials != null && credentials.Deleted,
                ValidAfter = credentials != null ? credentials.SessionsValidAfter : null,
            }
        )
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (session is null || session.Deleted)
        {
            return false;
        }

        return session.ValidAfter is null
            || (TryReadIssuedAt(principal, out var issuedAt) && issuedAt >= session.ValidAfter.Value);
    }

    private static bool TryReadIssuedAt(ClaimsPrincipal principal, out DateTimeOffset issuedAt)
    {
        if (
            long.TryParse(
                principal.FindFirstValue(SessionClaims.IssuedAtMilliseconds),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var milliseconds
            )
        )
        {
            issuedAt = DateTimeOffset.FromUnixTimeMilliseconds(milliseconds);
            return true;
        }

        // Tokens issued before the millisecond claim existed only carry iat (seconds).
        if (
            long.TryParse(
                principal.FindFirstValue("iat"),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out var seconds
            )
        )
        {
            issuedAt = DateTimeOffset.FromUnixTimeSeconds(seconds);
            return true;
        }

        issuedAt = default;
        return false;
    }
}
