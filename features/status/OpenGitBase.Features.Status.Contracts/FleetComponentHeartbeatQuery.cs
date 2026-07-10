using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Status.Contracts;

public sealed class FleetComponentHeartbeatQuery
    : IQuery<FleetComponentHeartbeatResult, FleetComponentHeartbeatQuery>
{
    public FleetComponentType ComponentType { get; set; }

    public string InstanceId { get; set; } = string.Empty;
}
