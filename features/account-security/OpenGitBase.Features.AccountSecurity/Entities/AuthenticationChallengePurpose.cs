namespace OpenGitBase.Features.AccountSecurity.Entities;

public enum AuthenticationChallengePurpose
{
    /// <summary>The password (or Google/Apple) step succeeded; the second factor is still owed.</summary>
    TwoFactorSignIn = 1,

    /// <summary>The user confirmed their identity again within a session, for a security change.</summary>
    ReAuthentication = 2,
}
