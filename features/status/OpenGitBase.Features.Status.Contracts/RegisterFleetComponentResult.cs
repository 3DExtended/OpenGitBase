namespace OpenGitBase.Features.Status.Contracts;

public sealed class RegisterFleetComponentResult
{
    public FleetComponentId FleetComponentId { get; set; } = default!;

    public int HeartbeatIntervalSeconds { get; set; }
}
