using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Services;

/// <summary>
/// Scores and selects storage nodes for encrypted replica slots.
/// Prefers <see cref="HostingScope.CrossOrgAllowed"/> nodes owned by a different org,
/// then platform nodes, while enforcing per-node capacity.
/// </summary>
public static class EncryptedReplicaPlacementEngine
{
    private const long CrossOrgTierBonus = 1_000_000_000_000L;
    private const long PlatformTierBonus = 100_000_000_000L;
    private const long RepositoryCountPenalty = 1_000_000L;

    public static long ScoreNode(
        StorageNodeDto node,
        Guid? repoOwnerOrganizationId,
        long repoMaxBytes,
        int repositoryCount
    )
    {
        if (!node.IsHealthy || !StorageNodeCapacity.HasCapacity(node, repoMaxBytes))
        {
            return long.MinValue;
        }

        var tierBonus = Classify(node, repoOwnerOrganizationId);
        if (tierBonus < 0)
        {
            return long.MinValue;
        }

        return tierBonus + node.FreeBytesAvailable - (repositoryCount * RepositoryCountPenalty);
    }

    public static StorageNodeDto? SelectBest(
        IReadOnlyList<StorageNodeDto> candidates,
        Guid? repoOwnerOrganizationId,
        long repoMaxBytes,
        IReadOnlyCollection<StorageNodeId> excluded,
        IReadOnlyDictionary<Guid, int>? repositoryCounts
    )
    {
        return candidates
            .Where(node => !excluded.Contains(node.Id))
            .Select(node =>
            {
                var count = repositoryCounts?.GetValueOrDefault(node.Id.Value) ?? 0;
                return (Node: node, Score: ScoreNode(node, repoOwnerOrganizationId, repoMaxBytes, count));
            })
            .Where(entry => entry.Score > long.MinValue)
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.Node.NodeId, StringComparer.Ordinal)
            .Select(entry => entry.Node)
            .FirstOrDefault();
    }

    internal static long Classify(StorageNodeDto node, Guid? repoOwnerOrganizationId)
    {
        if (node.OwnerOrganizationId is null)
        {
            return PlatformTierBonus;
        }

        if (node.OwnerOrganizationId == repoOwnerOrganizationId)
        {
            return long.MinValue;
        }

        if (node.HostingScope != HostingScope.CrossOrgAllowed)
        {
            return long.MinValue;
        }

        return CrossOrgTierBonus;
    }
}
