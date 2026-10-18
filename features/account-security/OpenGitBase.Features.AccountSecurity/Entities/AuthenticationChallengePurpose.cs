namespace OpenGitBase.Features.AccountSecurity.Entities;

public enum AuthenticationChallengePurpose
{
    /// <summary>The password (or Google/Apple) step succeeded; the second factor is still owed.</summary>
    TwoFactorSignIn = 1,

    /// <summary>The user confirmed their identity again within a session, for a security change.</summary>
    ReAuthentication = 2,

    /// <summary>Creation options for a new passkey were handed out; the attestation is still owed.</summary>
    PasskeyRegistration = 3,

    /// <summary>Request options for re-authenticating with a passkey were handed out.</summary>
    PasskeyReauthentication = 4,

    /// <summary>Request options for signing in with any passkey were handed out; nobody is known yet.</summary>
    PasskeySignIn = 5,

    /// <summary>Request options for answering an open two-factor sign-in with a passkey were handed out.</summary>
    PasskeySecondFactor = 6,
}
