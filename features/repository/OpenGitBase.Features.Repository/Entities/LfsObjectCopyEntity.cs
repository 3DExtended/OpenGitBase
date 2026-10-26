using OpenGitBase.Features.Repository.Contracts;

namespace OpenGitBase.Features.Repository.Entities;

/// <summary>
/// A storage node's copy of a recorded LFS object. Bookkeeping for routing and dashboards: the reconciler trusts
/// the on-disk inventory, not these rows.
/// </summary>
public class LfsObjectCopyEntity
{
    public Guid RepositoryId { get; set; }

    public string Oid { get; set; } = string.Empty;

    public Guid StorageNodeId { get; set; }

    public LfsObjectCopyKind Kind { get; set; }

    public DateTimeOffset StoredAt { get; set; }

    /// <summary>Last time an inventory of the node saw the copy on disk.</summary>
    public DateTimeOffset? LastVerifiedAt { get; set; }

    public LfsObjectEntity? Object { get; set; }
}
