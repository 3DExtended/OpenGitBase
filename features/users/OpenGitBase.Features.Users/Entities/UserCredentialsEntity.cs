using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Users.Entities;

public class UserCredentialsEntity : IIdentifiableEntity<Guid>
{
    [Key]
    [ForeignKey(nameof(User))]
    public Guid UserId { get; set; }

    public Guid Id => UserId;

    public UserEntity User { get; set; } = null!;

    public string Username { get; set; } = string.Empty;

    public string? PasswordHash { get; set; }

    public bool SignInProvider { get; set; }

    public string? InternalId { get; set; }

    public string? EmailCiphertext { get; set; }

    public string? EmailLookupHash { get; set; }

    public string? PasswordResetTokenHash { get; set; }

    public DateTimeOffset? PasswordResetTokenExpireDate { get; set; }

    public bool EmailVerified { get; set; }

    public string? EmailVerificationTokenHash { get; set; }

    public DateTimeOffset? EmailVerificationTokenExpireDate { get; set; }

    public bool Deleted { get; set; }

    /// <summary>Sign-in tokens issued before this instant are rejected (set on password reset/change).</summary>
    public DateTimeOffset? SessionsValidAfter { get; set; }

    /// <summary>When two-factor sign-in was turned on; null while it is off.</summary>
    public DateTimeOffset? TwoFactorEnabledAt { get; set; }

    /// <summary>Consecutive wrong second-factor attempts since the last success or lockout.</summary>
    public int SecondFactorFailedAttempts { get; set; }

    /// <summary>The second sign-in step is refused until this instant.</summary>
    public DateTimeOffset? SecondFactorLockedUntil { get; set; }
}
