namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

public enum PasskeyDeletionStatus
{
    Deleted = 1,

    /// <summary>
    /// 2FA is on and this passkey is the only second factor left (no other passkey, no authenticator
    /// app); turning 2FA off is the way out.
    /// </summary>
    LastSecondFactor = 2,
}
