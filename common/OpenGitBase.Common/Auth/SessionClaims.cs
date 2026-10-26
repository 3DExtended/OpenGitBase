using System.Globalization;
using System.Security.Claims;

namespace OpenGitBase.Common.Auth;

public static class SessionClaims
{
    /// <summary>
    /// Issue time in Unix milliseconds. The standard iat claim only has second precision, which
    /// cannot tell a session revoked during a password change from the one issued right after it.
    /// </summary>
    public const string IssuedAtMilliseconds = "ogb_iat_ms";

    /// <summary>The session's unique id (<c>jti</c>); re-authentication is bound to it.</summary>
    public static string? ReadSessionId(ClaimsPrincipal principal) => principal.FindFirstValue("jti");

    public static bool TryReadIssuedAt(ClaimsPrincipal principal, out DateTimeOffset issuedAt)
    {
        if (
            long.TryParse(
                principal.FindFirstValue(IssuedAtMilliseconds),
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
