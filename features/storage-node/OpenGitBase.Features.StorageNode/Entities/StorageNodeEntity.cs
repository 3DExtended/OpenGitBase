using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.StorageNode.Entities;

public class StorageNodeEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }

    public string NodeId { get; set; } = string.Empty;

    public string InternalHost { get; set; } = string.Empty;

    public int InternalSshPort { get; set; } = 22;

    public int InternalHttpPort { get; set; }

    public string ApiTokenHash { get; set; } = string.Empty;

    public string ApiTokenProtected { get; set; } = string.Empty;

    public long FreeBytesAvailable { get; set; }

    public long TotalBytesAvailable { get; set; }

    public DateTimeOffset? LastHeartbeatAt { get; set; }

    public bool IsHealthy { get; set; }

    public DateTimeOffset RegisteredAt { get; set; }

    public string CertificateThumbprint { get; set; } = string.Empty;
}
