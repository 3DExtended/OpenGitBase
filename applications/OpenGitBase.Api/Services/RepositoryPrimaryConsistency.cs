using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Api.Services;

/// <summary>
/// A repository names its primary twice. <c>PrimaryStorageNodeId</c> (and <c>StorageNodeId</c>, which should equal it)
/// is what routing, the push and clone gate, watermark commits, failover and rebalance go by; the replica row with
/// role Primary is what tells the storage node it is the primary, and what the reconciler's pending-writes step reads.
/// When the two name different nodes, routing finds no plaintext primary and every clone and push is denied.
/// </summary>
public static class RepositoryPrimaryConsistency
{
    /// <summary>What is inconsistent about the repository's primary; empty when both name one node.</summary>
    public static IReadOnlyList<string> FindProblems(RepositoryEntity repository)
    {
        var problems = new List<string>();
        var pointedRow = repository.Replicas.FirstOrDefault(replica =>
            replica.StorageNodeId == repository.PrimaryStorageNodeId
        );
        if (pointedRow is null)
        {
            problems.Add("No replica row is on the node PrimaryStorageNodeId names.");
        }
        else if (pointedRow.Role != RepositoryReplicaRole.Primary)
        {
            problems.Add($"The replica row on the node PrimaryStorageNodeId names has role {pointedRow.Role}, not Primary.");
        }

        var primaryRows = repository.Replicas.Count(replica => replica.Role == RepositoryReplicaRole.Primary);
        if (primaryRows != 1)
        {
            problems.Add($"{primaryRows} replica rows have role Primary instead of exactly one.");
        }

        if (repository.StorageNodeId != repository.PrimaryStorageNodeId)
        {
            problems.Add("StorageNodeId differs from PrimaryStorageNodeId.");
        }

        return problems;
    }
}
