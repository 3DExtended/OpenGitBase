using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.StorageNode.Contracts;

public sealed class CreateStorageNodeEnrollmentQuery
    : IQuery<CreateStorageNodeEnrollmentResult, CreateStorageNodeEnrollmentQuery>
{
    public string NodeId { get; set; } = string.Empty;

    public Guid CreatedByUserId { get; set; }

    public int ExpiresInHours { get; set; } = 168;

    public Guid? OrganizationId { get; set; }

    public long MaxBytes { get; set; }

    public HostingScope HostingScope { get; set; } = HostingScope.OwnOrgOnly;
}
