namespace OpenGitBase.Api.Models;

public class TwoFactorSignInDto
{
    public string ChallengeToken { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;
}
