namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

public enum PasskeyRegistrationStatus
{
    Registered = 1,

    /// <summary>No open registration challenge of this user matches: never issued, used or expired.</summary>
    ChallengeExpired = 2,

    /// <summary>The attestation failed verification: origin, RP ID, user verification or signature.</summary>
    Rejected = 3,

    /// <summary>This credential is already registered.</summary>
    AlreadyRegistered = 4,
}
