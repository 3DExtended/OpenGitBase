namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

public enum TwoFactorSignInStatus
{
    Succeeded = 1,
    InvalidCode = 2,

    /// <summary>The challenge is unknown, used up or older than five minutes; sign in again.</summary>
    ChallengeExpired = 3,

    /// <summary>Too many wrong second-factor attempts in a row; see <c>LockedUntil</c>.</summary>
    LockedOut = 4,
}
