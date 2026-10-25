namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

public enum TwoFactorPasskeyOptionsStatus
{
    Issued = 1,

    /// <summary>The sign-in challenge is unknown, used up or older than five minutes; sign in again.</summary>
    ChallengeExpired = 2,

    NoPasskeys = 3,
}
