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
}
