namespace OpenGitBase.Api.Models;

public sealed class RegisterFleetComponentResponse
{
    public Guid FleetComponentId { get; set; }

    public int HeartbeatIntervalSeconds { get; set; }
}
