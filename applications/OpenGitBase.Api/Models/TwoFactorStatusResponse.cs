namespace OpenGitBase.Api.Models;

/// <summary>Whether two-factor sign-in is on after a confirm or disable.</summary>
public class TwoFactorStatusResponse
{
    public bool TwoFactorEnabled { get; set; }

    /// <summary>Only on the confirmation that turned 2FA on: the first recovery codes, shown once.</summary>
    public IReadOnlyList<string>? RecoveryCodes { get; set; }
}
