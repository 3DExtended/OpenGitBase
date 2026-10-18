namespace OpenGitBase.Common.Options;

/// <summary>Two-factor sign-in settings (section <c>TwoFactor</c>).</summary>
public sealed class TwoFactorOptions
{
    /// <summary>
    /// Off by default: nobody can turn 2FA on. Accounts that already have it keep the second sign-in
    /// step and can still turn it off, so switching this off never weakens an account.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>The name authenticator apps show next to the account.</summary>
    public string Issuer { get; set; } = "OpenGitBase";
}
