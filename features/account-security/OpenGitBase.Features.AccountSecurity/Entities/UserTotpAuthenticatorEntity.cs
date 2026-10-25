using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.AccountSecurity.Entities;

/// <summary>
/// A user's authenticator app. Secrets are encrypted byte for byte (EncryptSecret, never EncryptEmail,
/// which lowercases). A new setup waits in <see cref="PendingSecretProtected"/> and only replaces the
/// confirmed secret once a code from the new app has been entered.
/// </summary>
public class UserTotpAuthenticatorEntity
{
    public Guid UserId { get; set; }

    public UserEntity User { get; set; } = null!;

    /// <summary>The confirmed secret; null until the first setup is confirmed.</summary>
    public string? SecretProtected { get; set; }

    public DateTimeOffset? ConfirmedAt { get; set; }

    /// <summary>A setup that has not been confirmed yet.</summary>
    public string? PendingSecretProtected { get; set; }

    /// <summary>The last accepted 30-second step; codes of this step or earlier are replays.</summary>
    public long LastUsedTimeStep { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
