using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Status.Contracts;

public class FleetComponentDto : ModelBase<FleetComponentId, Guid>
{
    public FleetComponentType ComponentType { get; set; }

    public string InstanceId { get; set; } = string.Empty;

    public string ProbeUrl { get; set; } = string.Empty;

    public DateTimeOffset RegisteredAt { get; set; }

    public DateTimeOffset? LastHeartbeatAt { get; set; }

    public bool IsHealthy { get; set; }

    public string? Version { get; set; }
}
