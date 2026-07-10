using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Features.StorageNode.Entities;

public class StorageNodeEnrollmentEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }

    public string NodeId { get; set; } = string.Empty;

    public string EnrollmentTokenHash { get; set; } = string.Empty;

    public Guid CreatedByUserId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? ConsumedAt { get; set; }

    public Guid? OrganizationId { get; set; }

    public long MaxBytes { get; set; }

    public HostingScope HostingScope { get; set; } = HostingScope.OwnOrgOnly;
}
