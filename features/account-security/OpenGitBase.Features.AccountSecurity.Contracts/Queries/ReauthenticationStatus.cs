namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

public enum ReauthenticationStatus
{
    Succeeded = 1,
    InvalidCredentials = 2,
    SecondFactorRequired = 3,
    InvalidCode = 4,
    LockedOut = 5,

    /// <summary>The account has neither a password nor a second factor to confirm with.</summary>
    Unavailable = 6,
}
