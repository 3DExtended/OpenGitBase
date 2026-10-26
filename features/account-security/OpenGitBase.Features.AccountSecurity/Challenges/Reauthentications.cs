namespace OpenGitBase.Features.AccountSecurity.Challenges;

/// <summary>
/// A re-authentication is a <c>ReAuthentication</c> challenge bound to one user's session, so a
/// confirmation made in one session does not unlock sensitive changes in another.
/// </summary>
internal static class Reauthentications
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(10);

    public static string Binding(Guid userId, string sessionId) => $"{userId:N}:{sessionId}";
}
