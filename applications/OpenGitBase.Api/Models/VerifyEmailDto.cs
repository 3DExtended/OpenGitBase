namespace OpenGitBase.Api.Models;

public class VerifyEmailDto
{
    public string Username { get; set; } = string.Empty;

    public string VerificationToken { get; set; } = string.Empty;
}
