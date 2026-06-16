namespace OpenGitBase.Api.Models;

public class AccountMeResponse
{
    public string Username { get; set; } = string.Empty;

    public bool EmailVerified { get; set; }

    public bool IsAdmin { get; set; }

    public AccountDebugFeaturesDto? Debug { get; set; }
}
