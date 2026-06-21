using OpenGitBase.Cqrs;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Features.Repository.Contracts;

public sealed class CommitReplicationWatermarkQuery
    : IQuery<CommitReplicationWatermarkResult, CommitReplicationWatermarkQuery>
{
    public RepositoryId RepositoryId { get; set; } = default!;

    public StorageNodeId StorageNodeId { get; set; } = default!;

    public long ReplicationEpoch { get; set; }

    public long NewWatermark { get; set; }

    public IReadOnlyList<StorageNodeId> QuorumNodeIds { get; set; } = [];
}
