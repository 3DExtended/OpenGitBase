using OpenGitBase.Common.Storage;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Services;

public static class ReplicaSetPlanner
{
    public const int RequiredHealthyNodes = 3;

    public static ReplicaSetSelection? SelectReplicaSet(IReadOnlyList<StorageNodeDto> healthyNodes)
    {
        if (healthyNodes.Count < RequiredHealthyNodes)
        {
            return null;
        }

        var ordered = healthyNodes
            .OrderByDescending(node => node.FreeBytesAvailable)
            .ThenBy(node => node.NodeId, StringComparer.Ordinal)
            .ToList();

        var byNodeId = ordered.ToDictionary(node => node.NodeId, StringComparer.Ordinal);

        var primaryAndRead =
            byNodeId.GetValueOrDefault(PlatformRf4FleetLayout.PrimaryAndReadNodeId) ?? ordered[0];

        var encryptedA = SelectEncryptedNode(
            ordered,
            preferredNodeId: PlatformRf4FleetLayout.EncryptedReplicaNodeIdA,
            excluded: [primaryAndRead.Id]
        );
        var encryptedB = SelectEncryptedNode(
            ordered,
            preferredNodeId: PlatformRf4FleetLayout.EncryptedReplicaNodeIdB,
            excluded: [primaryAndRead.Id, encryptedA.Id]
        );

        return new ReplicaSetSelection(primaryAndRead, primaryAndRead, encryptedA, encryptedB);
    }

    private static StorageNodeDto SelectEncryptedNode(
        IReadOnlyList<StorageNodeDto> ordered,
        string preferredNodeId,
        IReadOnlyCollection<StorageNodeId> excluded
    )
    {
        var preferred = ordered.FirstOrDefault(node =>
            string.Equals(node.NodeId, preferredNodeId, StringComparison.Ordinal)
            && !excluded.Contains(node.Id)
        );
        if (preferred is not null)
        {
            return preferred;
        }

        return ordered.First(node => !excluded.Contains(node.Id));
    }
}
