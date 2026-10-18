namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

public enum PasskeySignInStatus
{
    SignedIn = 1,

    /// <summary>Unknown credential, deleted user, failed verification, or a used or expired challenge.</summary>
    Rejected = 2,
}
