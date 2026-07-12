using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.ComputeNode.Contracts;

namespace OpenGitBase.Features.ComputeNode.Entities;

public sealed class ComputeNodeEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }

    public string NodeId { get; set; } = string.Empty;

    public Guid? OrganizationId { get; set; }

    public ComputeHostingScope HostingScope { get; set; }

    public int MaxConcurrentJobs { get; set; }

    public int RunningJobs { get; set; }

    public int MaxCpu { get; set; }

    public long MaxMemoryBytes { get; set; }

    public bool IsHealthy { get; set; }

    public DateTimeOffset RegisteredAt { get; set; }

    public DateTimeOffset? LastHeartbeatAt { get; set; }

    public string? IdentityTokenHash { get; set; }
}
