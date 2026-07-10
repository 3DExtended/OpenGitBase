using OpenGitBase.Common.Storage;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Services;

public static class ReplicaSetPlanner
{
    public const int RequiredHealthyNodes = 3;

    public static ReplicaSetSelection? SelectReplicaSet(IReadOnlyList<StorageNodeDto> healthyNodes) =>
        SelectReplicaSet(new ReplicaSetPlannerRequest(healthyNodes));

    public static ReplicaSetSelection? SelectReplicaSet(ReplicaSetPlannerRequest request)
    {
        var nodes = request
            .HealthyNodes.Where(node => node.IsHealthy)
            .Where(node => StorageNodeCapacity.HasCapacity(node, request.RequiredBytesPerNode))
            .ToList();

        if (request.OwnerOrganizationId is null)
        {
            return SelectPlatformDefault(FilterPlatformNodes(nodes));
        }

        var orgId = request.OwnerOrganizationId.Value;
        var orgNodes = nodes
            .Where(node => node.OwnerOrganizationId == orgId)
            .OrderByDescending(node => node.FreeBytesAvailable)
            .ThenBy(node => node.NodeId, StringComparer.Ordinal)
            .ToList();

        var tier = ResolveTier(request, orgNodes.Count);
        if (tier < 0)
        {
            return null;
        }

        var platformNodes = FilterPlatformNodes(nodes);
        var externalEncrypted = nodes
            .Where(node => node.OwnerOrganizationId != orgId)
            .Where(node =>
                node.OwnerOrganizationId is null
                || node.HostingScope == HostingScope.CrossOrgAllowed
            )
            .OrderByDescending(node => node.OwnerOrganizationId is null)
            .ThenByDescending(node => node.FreeBytesAvailable)
            .ThenBy(node => node.NodeId, StringComparer.Ordinal)
            .ToList();

        return tier switch
        {
            0 => SelectPlatformDefault(platformNodes) ?? SelectPlatformDefault(nodes),
            1 => SelectTier1(orgNodes, externalEncrypted),
            2 => SelectTier2(orgNodes, externalEncrypted),
            3 => SelectTier3(orgNodes),
            _ => null,
        };
    }

    internal static int ResolveTier(ReplicaSetPlannerRequest request, int orgNodeCount)
    {
        if (request.PlacementPolicy == PlacementPolicy.PlatformDefault)
        {
            return 0;
        }

        if (
            request.SelfHostPreference == SelfHostPreference.PlatformOnly
            && request.PlacementPolicy != PlacementPolicy.MaxSelfHost
        )
        {
            return 0;
        }

        if (orgNodeCount == 0)
        {
            return request.SelfHostPreference == SelfHostPreference.RequireSelfHost
                    || request.PlacementPolicy == PlacementPolicy.MaxSelfHost
                ? -1
                : 0;
        }

        if (
            request.PlacementPolicy == PlacementPolicy.MaxSelfHost
            || request.SelfHostPreference is SelfHostPreference.PreferSelfHost
                or SelfHostPreference.RequireSelfHost
        )
        {
            return Math.Min(3, orgNodeCount);
        }

        return 0;
    }

    private static IReadOnlyList<StorageNodeDto> FilterPlatformNodes(
        IReadOnlyList<StorageNodeDto> nodes
    ) => nodes.Where(node => node.OwnerOrganizationId is null).ToList();

    private static ReplicaSetSelection? SelectPlatformDefault(
        IReadOnlyList<StorageNodeDto> healthyNodes
    )
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

    private static ReplicaSetSelection? SelectTier1(
        IReadOnlyList<StorageNodeDto> orgNodes,
        IReadOnlyList<StorageNodeDto> externalEncrypted
    )
    {
        if (orgNodes.Count < 1 || externalEncrypted.Count < 2)
        {
            return null;
        }

        var primaryAndRead = orgNodes[0];
        var encryptedA = SelectExternalEncrypted(externalEncrypted, excluded: [primaryAndRead.Id]);
        var encryptedB = SelectExternalEncrypted(
            externalEncrypted,
            excluded: [primaryAndRead.Id, encryptedA.Id]
        );
        return new ReplicaSetSelection(primaryAndRead, primaryAndRead, encryptedA, encryptedB);
    }

    private static ReplicaSetSelection? SelectTier2(
        IReadOnlyList<StorageNodeDto> orgNodes,
        IReadOnlyList<StorageNodeDto> externalEncrypted
    )
    {
        if (orgNodes.Count < 2 || externalEncrypted.Count < 1)
        {
            return null;
        }

        var primaryAndRead = orgNodes[0];
        var orgEncrypted = orgNodes[1];
        var external = SelectExternalEncrypted(
            externalEncrypted,
            excluded: [primaryAndRead.Id, orgEncrypted.Id]
        );
        return new ReplicaSetSelection(primaryAndRead, primaryAndRead, orgEncrypted, external);
    }

    private static ReplicaSetSelection? SelectTier3(IReadOnlyList<StorageNodeDto> orgNodes)
    {
        if (orgNodes.Count < 3)
        {
            return null;
        }

        var primaryAndRead = orgNodes[0];
        var encryptedA = orgNodes[1];
        var encryptedB = orgNodes[2];
        return new ReplicaSetSelection(primaryAndRead, primaryAndRead, encryptedA, encryptedB);
    }

    private static StorageNodeDto SelectExternalEncrypted(
        IReadOnlyList<StorageNodeDto> candidates,
        IReadOnlyCollection<StorageNodeId> excluded
    )
    {
        var preferredPlatform = candidates.FirstOrDefault(node =>
            node.OwnerOrganizationId is null && !excluded.Contains(node.Id)
        );
        if (preferredPlatform is not null)
        {
            return preferredPlatform;
        }

        return candidates.First(node => !excluded.Contains(node.Id));
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
