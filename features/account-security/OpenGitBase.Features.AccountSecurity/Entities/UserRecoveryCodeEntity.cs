using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.AccountSecurity.Entities;

/// <summary>
/// One single-use recovery code. Only a <c>PasswordHasher</c> hash is stored: a code carries 50 random
/// bits, little enough that a plain SHA-256 lookup hash could be reversed by brute force.
/// </summary>
public class UserRecoveryCodeEntity
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public UserEntity User { get; set; } = null!;

    public string CodeHash { get; set; } = string.Empty;

    /// <summary>Set when the code was spent; a used code is never accepted again.</summary>
    public DateTimeOffset? UsedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
