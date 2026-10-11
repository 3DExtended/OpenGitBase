using OpenGitBase.Cqrs;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Features.Repository.Contracts;

/// <summary>
/// Records on the primary replica's row (its LastSyncedAt) that a push is replicating the
/// repository right now. The anti-entropy reconciler leaves such a repository to the push for its
/// grace period, and a reconciler run that claimed it earlier stops before its next upload, sync
/// or commit. True when the primary's row was marked.
/// </summary>
public sealed class MarkPrimaryReplicatingQuery : IQuery<bool, MarkPrimaryReplicatingQuery>
{
    public RepositoryId RepositoryId { get; set; } = default!;

    public StorageNodeId StorageNodeId { get; set; } = default!;
}
