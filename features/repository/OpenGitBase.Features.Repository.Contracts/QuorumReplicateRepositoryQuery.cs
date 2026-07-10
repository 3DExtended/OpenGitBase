using OpenGitBase.Cqrs;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Features.Repository.Contracts;

public sealed class QuorumReplicateRepositoryQuery
    : IQuery<QuorumReplicateRepositoryResult, QuorumReplicateRepositoryQuery>
{
    public RepositoryId RepositoryId { get; set; } = default!;

    public StorageNodeId StorageNodeId { get; set; } = default!;

    public long AppliedWatermark { get; set; }

    public IReadOnlyList<Guid> ConfirmedEncryptedNodeIds { get; set; } = [];
}
