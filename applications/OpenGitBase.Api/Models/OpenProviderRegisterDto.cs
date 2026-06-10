namespace OpenGitBase.Api.Models;

public class OpenProviderRegisterDto
{
    public string Username { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string InternalId { get; set; } = string.Empty;

    public string? RegistrationToken { get; set; }
}
