namespace OpenGitBase.Features.AccountSecurity.Passkeys;

internal enum PasskeyAssertionOutcome
{
    Verified = 1,
    ChallengeExpired = 2,
    Rejected = 3,
}
