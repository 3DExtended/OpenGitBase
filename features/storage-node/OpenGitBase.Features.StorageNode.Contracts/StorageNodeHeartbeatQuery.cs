using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.StorageNode.Contracts;

public sealed class StorageNodeHeartbeatQuery
    : IQuery<StorageNodeHeartbeatResult, StorageNodeHeartbeatQuery>
{
    public string NodeId { get; set; } = string.Empty;

    public long FreeBytesAvailable { get; set; }

    public long TotalBytesAvailable { get; set; }

    public string CertificateThumbprint { get; set; } = string.Empty;
}
