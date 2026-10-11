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

    /// <summary>
    /// RF4 only: the API creates the encrypted artifact on the primary and uploads it to the
    /// encrypted replicas itself, instead of the post-receive hook building and relaying it.
    /// <see cref="ConfirmedEncryptedNodeIds"/> is ignored on this path.
    /// </summary>
    public bool CreateArtifact { get; set; }

    /// <summary>
    /// Set by the reconciler when it replicates writes the primary holds beyond PrimaryWatermark:
    /// the primary replica's LastSyncedAt it claimed. If a push touches the primary meanwhile, the
    /// run stops before uploading and leaves the replication to that push.
    /// </summary>
    public DateTimeOffset? ExpectedPrimaryLastSyncedAt { get; set; }
}
