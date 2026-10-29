namespace OpenGitBase.Features.StorageNode.Contracts;

/// <summary>
/// A single kind of storage/DB drift the fleet report can detect for a (node, repository) pair.
/// </summary>
public enum FleetDriftKind
{
    /// <summary>A plaintext bare repo is on disk on the node, but no plaintext replica record
    /// (Primary/Replica/ReadReplica) exists for it on that node.</summary>
    OrphanOnDisk,

    /// <summary>A plaintext replica record exists for the node, but the repo is not on disk there.</summary>
    MissingOnDisk,

    /// <summary>An encrypted artifact set is on disk on the node, but no EncryptedReplica record
    /// exists for it on that node.</summary>
    OrphanArtifact,

    /// <summary>An EncryptedReplica record exists for the node, but no artifact is on disk there.</summary>
    MissingArtifact,

    /// <summary>LFS data (plaintext or encrypted) is on disk on the node for a repository that no longer
    /// exists. The reconciler deletes it on its next pass.</summary>
    OrphanLfs,

    /// <summary>The repository's primary pointer and its Primary replica row disagree: the row on the node
    /// <c>PrimaryStorageNodeId</c> names is missing or not Primary, the repository has no or several Primary rows, or
    /// <c>StorageNodeId</c> differs from <c>PrimaryStorageNodeId</c>. Routing then finds no plaintext primary and
    /// denies every clone and push. Read from the database alone; reported on the node the pointer names.</summary>
    PrimaryMismatch,
}
