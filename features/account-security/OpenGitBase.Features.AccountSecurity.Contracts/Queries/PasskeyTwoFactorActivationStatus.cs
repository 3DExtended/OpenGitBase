namespace OpenGitBase.Features.AccountSecurity.Contracts.Queries;

public enum PasskeyTwoFactorActivationStatus
{
    Enabled = 1,
    NoPasskeys = 2,

    /// <summary>2FA is on already; nothing changed, the recovery codes stay.</summary>
    AlreadyEnabled = 3,
}
