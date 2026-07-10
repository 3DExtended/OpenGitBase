using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.StorageNode.Contracts;

public class StorageNodeDto : ModelBase<StorageNodeId, Guid>
{
    public string NodeId { get; set; } = string.Empty;

    public string InternalHost { get; set; } = string.Empty;

    public int InternalSshPort { get; set; } = 22;

    public int InternalHttpPort { get; set; }

    public int InternalGitHttpPort { get; set; } = 8082;

    public long FreeBytesAvailable { get; set; }

    public long TotalBytesAvailable { get; set; }

    public DateTimeOffset? LastHeartbeatAt { get; set; }

    public bool IsHealthy { get; set; }

    public DateTimeOffset RegisteredAt { get; set; }

    public string CertificateThumbprint { get; init; } = string.Empty;

    public Guid? OwnerOrganizationId { get; set; }

    public long MaxBytes { get; set; }

    public long UsedBytes { get; set; }

    public HostingScope HostingScope { get; set; } = HostingScope.OwnOrgOnly;
}
