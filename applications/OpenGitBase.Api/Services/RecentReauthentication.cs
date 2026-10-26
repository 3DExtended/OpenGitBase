using System.Security.Claims;
using OpenGitBase.Common.Auth;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.AccountSecurity.Contracts.Queries;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Api.Services;

/// <summary>Whether the calling session re-authenticated in the last 10 minutes, as sensitive settings require.</summary>
public static class RecentReauthentication
{
    public static async Task<bool> HasAsync(
        IQueryProcessor queryProcessor,
        ClaimsPrincipal user,
        UserId userId,
        CancellationToken cancellationToken
    )
    {
        var sessionId = SessionClaims.ReadSessionId(user);
        if (string.IsNullOrEmpty(sessionId) || !SessionClaims.TryReadIssuedAt(user, out var issuedAt))
        {
            return false;
        }

        var result = await queryProcessor
            .RunQueryAsync(
                new HasRecentReauthenticationQuery
                {
                    UserId = userId,
                    SessionId = sessionId,
                    SessionIssuedAt = issuedAt,
                },
                cancellationToken
            )
            .ConfigureAwait(false);
        return result.IsSome && result.Get();
    }
}
