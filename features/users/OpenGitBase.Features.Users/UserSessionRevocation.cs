using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Users;

public static class UserSessionRevocation
{
    // Kept at millisecond precision to match the issue time carried by session tokens.
    public static void RevokeSessionsIssuedBefore(UserCredentialsEntity credentials, DateTimeOffset now) =>
        credentials.SessionsValidAfter = DateTimeOffset.FromUnixTimeMilliseconds(now.ToUnixTimeMilliseconds());
}
