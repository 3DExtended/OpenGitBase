using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.AccountSecurity.Entities;

/// <summary>
/// A short-lived, single-use step of an authentication ceremony, shared by every API replica.
/// <see cref="TokenHash"/> is the SHA-256 of what the row is bound to: the opaque token handed to the
/// client, or the session id for a re-authentication.
/// </summary>
public class AuthenticationChallengeEntity
{
    public Guid Id { get; set; }

    public AuthenticationChallengePurpose Purpose { get; set; }

    public Guid UserId { get; set; }

    public UserEntity User { get; set; } = null!;

    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
