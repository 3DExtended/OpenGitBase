using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Status.Contracts;

public sealed class RegisterFleetComponentQuery
    : IQuery<RegisterFleetComponentResult, RegisterFleetComponentQuery>
{
    public FleetComponentType ComponentType { get; set; }

    public string InstanceId { get; set; } = string.Empty;

    public string ProbeUrl { get; set; } = string.Empty;

    public string? Version { get; set; }
}
