namespace OpenGitBase.Api.Models;

/// <summary>The second sign-in step: exactly one of <see cref="Code"/> and <see cref="RecoveryCode"/>.</summary>
public class TwoFactorSignInDto
{
    public string ChallengeToken { get; set; } = string.Empty;

    /// <summary>The authenticator code.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>One of the account's recovery codes, with or without the dash, in any case.</summary>
    public string? RecoveryCode { get; set; }
}
