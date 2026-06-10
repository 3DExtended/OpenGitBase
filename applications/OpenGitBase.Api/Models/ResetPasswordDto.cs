namespace OpenGitBase.Api.Models;

public class ResetPasswordDto
{
    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string ResetCode { get; set; } = string.Empty;

    public string NewPassword { get; set; } = string.Empty;
}
