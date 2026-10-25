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

    /// <summary>The passkey assertion failed verification or is not for one of the user's passkeys.</summary>
    PasskeyRejected = 7,

    /// <summary>No open passkey challenge of this user matches: never issued, used or expired.</summary>
    ChallengeExpired = 8,
}
