namespace OpenGitBase.Common.Auth;

public static class SessionClaims
{
    /// <summary>
    /// Issue time in Unix milliseconds. The standard iat claim only has second precision, which
    /// cannot tell a session revoked during a password change from the one issued right after it.
    /// </summary>
    public const string IssuedAtMilliseconds = "ogb_iat_ms";
}
