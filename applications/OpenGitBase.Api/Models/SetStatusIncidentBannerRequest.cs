namespace OpenGitBase.Api.Models;

public sealed class SetStatusIncidentBannerRequest
{
    public string Message { get; set; } = string.Empty;

    public string Severity { get; set; } = "info";
}
