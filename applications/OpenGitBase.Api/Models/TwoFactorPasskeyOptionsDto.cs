namespace OpenGitBase.Api.Models;

public class TwoFactorPasskeyOptionsDto
{
    /// <summary>The token from the first sign-in step.</summary>
    public string ChallengeToken { get; set; } = string.Empty;
}
