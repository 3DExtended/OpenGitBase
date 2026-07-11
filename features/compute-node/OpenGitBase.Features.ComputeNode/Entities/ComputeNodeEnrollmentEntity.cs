using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.ComputeNode.Contracts;

namespace OpenGitBase.Features.ComputeNode.Entities;

public sealed class ComputeNodeEnrollmentEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }

    public string NodeId { get; set; } = string.Empty;

    public string EnrollmentTokenHash { get; set; } = string.Empty;

    public Guid CreatedByUserId { get; set; }

    public Guid? OrganizationId { get; set; }

    public ComputeHostingScope HostingScope { get; set; }

    public int MaxConcurrentJobs { get; set; }

    public int MaxCpu { get; set; }

    public long MaxMemoryBytes { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? ConsumedAt { get; set; }
}
