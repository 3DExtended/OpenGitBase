using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Status.Contracts;

namespace OpenGitBase.Features.Status.Entities;

public class FleetComponentEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }

    public FleetComponentType ComponentType { get; set; }

    public string InstanceId { get; set; } = string.Empty;

    public string ProbeUrl { get; set; } = string.Empty;

    public DateTimeOffset RegisteredAt { get; set; }

    public DateTimeOffset? LastHeartbeatAt { get; set; }

    public bool IsHealthy { get; set; }

    public string? Version { get; set; }
}
