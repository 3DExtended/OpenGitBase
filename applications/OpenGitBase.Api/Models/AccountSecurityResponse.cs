namespace OpenGitBase.Api.Models;

public class AccountSecurityResponse
{
    /// <summary>Whether 2FA can be turned on here (<c>TwoFactor:Enabled</c>).</summary>
    public bool TwoFactorAvailable { get; set; }

    public bool TwoFactorEnabled { get; set; }

    public bool TotpConfigured { get; set; }

    /// <summary>False for Google/Apple-only accounts; they re-authenticate by signing in again.</summary>
    public bool HasPassword { get; set; }

    /// <summary>Unused recovery codes.</summary>
    public int RecoveryCodesRemaining { get; set; }
}
