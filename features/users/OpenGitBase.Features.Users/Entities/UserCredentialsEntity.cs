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
}
