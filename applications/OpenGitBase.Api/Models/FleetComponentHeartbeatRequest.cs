namespace OpenGitBase.Api.Models;

public sealed class FleetComponentHeartbeatRequest
{
    public string ComponentType { get; set; } = string.Empty;

    public string InstanceId { get; set; } = string.Empty;
}
