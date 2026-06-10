namespace OpenGitBase.Common.Auth;

public class UserIdentity
{
    public string? IdentityProviderId { get; set; }

    public Guid UserId =>
        Guid.Parse(
            IdentityProviderId
                ?? throw new InvalidOperationException("IdentityProviderId is required for UserId.")
        );

    public string? Username { get; set; }
}
