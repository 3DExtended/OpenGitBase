using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.ComputeNode.Contracts;

public enum ComputeHostingScope
{
    OwnOrgOnly = 0,
    CrossOrgAllowed = 1,
}

public sealed record ComputeNodeId : Identifier<Guid, ComputeNodeId>;

public sealed class ComputeNodeDto
{
    public ComputeNodeId Id { get; set; } = ComputeNodeId.From(Guid.NewGuid());

    public string NodeId { get; set; } = string.Empty;

    public Guid? OrganizationId { get; set; }

    public ComputeHostingScope HostingScope { get; set; }

    public int MaxConcurrentJobs { get; set; }

    public int RunningJobs { get; set; }

    public int MaxCpu { get; set; }

    public long MaxMemoryBytes { get; set; }

    public bool IsHealthy { get; set; }

    public DateTimeOffset? LastHeartbeatAt { get; set; }
}

public sealed class CreateComputeNodeEnrollmentQuery
    : IQuery<string, CreateComputeNodeEnrollmentQuery>
{
    public string NodeId { get; set; } = string.Empty;

    public Guid CreatedByUserId { get; set; }

    public Guid? OrganizationId { get; set; }

    public ComputeHostingScope HostingScope { get; set; } = ComputeHostingScope.OwnOrgOnly;

    public int MaxConcurrentJobs { get; set; } = 1;

    public int MaxCpu { get; set; } = 1;

    public long MaxMemoryBytes { get; set; } = 2L * 1024 * 1024 * 1024;
}

public sealed class RegisterComputeNodeQuery : IQuery<ComputeNodeDto, RegisterComputeNodeQuery>
{
    public string NodeId { get; set; } = string.Empty;

    public string EnrollmentToken { get; set; } = string.Empty;
}

public sealed class ComputeNodeHeartbeatQuery : IQuery<ComputeNodeDto, ComputeNodeHeartbeatQuery>
{
    public string NodeId { get; set; } = string.Empty;

    public int RunningJobs { get; set; }
}

public sealed class UpdateComputeNodeCapacityQuery
    : IQuery<ComputeNodeDto, UpdateComputeNodeCapacityQuery>
{
    public ComputeNodeId ComputeNodeId { get; set; } = ComputeNodeId.From(Guid.NewGuid());

    public int MaxConcurrentJobs { get; set; }

    public int MaxCpu { get; set; }

    public long MaxMemoryBytes { get; set; }
}

public sealed class ListComputeNodesQuery : IQuery<IReadOnlyList<ComputeNodeDto>, ListComputeNodesQuery>
{
    public Guid? OrganizationId { get; set; }
}
