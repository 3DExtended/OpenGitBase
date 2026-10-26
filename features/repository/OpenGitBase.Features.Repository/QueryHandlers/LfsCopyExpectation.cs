using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Features.Repository.QueryHandlers;

/// <summary>Which nodes should hold a copy of each of a repository's LFS objects, and of which kind.</summary>
internal static class LfsCopyExpectation
{
    /// <summary>Every replica (encrypted replicas hold encrypted copies, the others plaintext), and the primary even
    /// when it has no replica row.</summary>
    public static Dictionary<Guid, LfsObjectCopyKind> Nodes(
        IEnumerable<(Guid StorageNodeId, RepositoryReplicaRole Role)> replicas,
        Guid? primaryStorageNodeId
    )
    {
        var nodes = replicas
            .GroupBy(replica => replica.StorageNodeId)
            .ToDictionary(
                group => group.Key,
                group => group.Any(replica => replica.Role == RepositoryReplicaRole.EncryptedReplica)
                    ? LfsObjectCopyKind.Encrypted
                    : LfsObjectCopyKind.Plaintext
            );
        if (primaryStorageNodeId is { } primary)
        {
            nodes.TryAdd(primary, LfsObjectCopyKind.Plaintext);
        }

        return nodes;
    }
}
