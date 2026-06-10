using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Users.Contracts.Models;

public class UserCredentials : ModelBase<UserCredentialsId, Guid>
{
    public string Username { get; set; } = string.Empty;

    public string? PasswordHash { get; set; }

    public bool SignInProvider { get; set; }

    public string? InternalId { get; set; }

    public string? EmailCiphertext { get; set; }

    public string? EmailLookupHash { get; set; }

    public string? PasswordResetTokenHash { get; set; }

    public DateTimeOffset? PasswordResetTokenExpireDate { get; set; }

    public bool Deleted { get; set; }
}
