namespace OpenGitBase.Api.Models;

/// <summary>Whether two-factor sign-in is on after a confirm or disable.</summary>
public class TwoFactorStatusResponse
{
    public bool TwoFactorEnabled { get; set; }
}
