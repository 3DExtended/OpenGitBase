namespace OpenGitBase.Api.Models;

public sealed class RegisterFleetComponentRequest
{
    public string ComponentType { get; set; } = string.Empty;

    public string InstanceId { get; set; } = string.Empty;

    public string ProbeUrl { get; set; } = string.Empty;

    public string? Version { get; set; }
}
