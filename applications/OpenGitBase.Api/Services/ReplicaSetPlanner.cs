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
            return SelectPlatformDefault(
                FilterPlatformNodes(nodes),
                nodes,
                repoOwnerOrganizationId: null,
                request
            );
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
            .ToList();

        return tier switch
        {
            0 => SelectPlatformDefault(platformNodes, nodes, orgId, request)
                ?? SelectPlatformDefault(nodes, nodes, orgId, request),
            1 => SelectTier1(orgNodes, externalEncrypted, orgId, request),
            2 => SelectTier2(orgNodes, externalEncrypted, orgId, request),
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
        IReadOnlyList<StorageNodeDto> platformNodes,
        IReadOnlyList<StorageNodeDto> encryptedCandidates,
        Guid? repoOwnerOrganizationId,
        ReplicaSetPlannerRequest request
    )
    {
        if (platformNodes.Count < RequiredHealthyNodes)
        {
            return null;
        }

        var ordered = platformNodes
            .OrderByDescending(node => node.FreeBytesAvailable)
            .ThenBy(node => node.NodeId, StringComparer.Ordinal)
            .ToList();

        var byNodeId = ordered.ToDictionary(node => node.NodeId, StringComparer.Ordinal);

        var primaryAndRead =
            byNodeId.GetValueOrDefault(PlatformRf4FleetLayout.PrimaryAndReadNodeId) ?? ordered[0];

        var encryptedA =
            SelectEncryptedReplica(
                encryptedCandidates,
                repoOwnerOrganizationId,
                request,
                excluded: [primaryAndRead.Id]
            )
            ?? SelectEncryptedNode(
                ordered,
                preferredNodeId: PlatformRf4FleetLayout.EncryptedReplicaNodeIdA,
                excluded: [primaryAndRead.Id]
            );

        var encryptedB =
            SelectEncryptedReplica(
                encryptedCandidates,
                repoOwnerOrganizationId,
                request,
                excluded: [primaryAndRead.Id, encryptedA.Id]
            )
            ?? SelectEncryptedNode(
                ordered,
                preferredNodeId: PlatformRf4FleetLayout.EncryptedReplicaNodeIdB,
                excluded: [primaryAndRead.Id, encryptedA.Id]
            );

        return new ReplicaSetSelection(primaryAndRead, primaryAndRead, encryptedA, encryptedB);
    }

    private static ReplicaSetSelection? SelectTier1(
        IReadOnlyList<StorageNodeDto> orgNodes,
        IReadOnlyList<StorageNodeDto> externalEncrypted,
        Guid repoOwnerOrganizationId,
        ReplicaSetPlannerRequest request
    )
    {
        if (orgNodes.Count < 1)
        {
            return null;
        }

        var primaryAndRead = orgNodes[0];
        var encryptedA = SelectEncryptedReplica(
            externalEncrypted,
            repoOwnerOrganizationId,
            request,
            excluded: [primaryAndRead.Id]
        );
        if (encryptedA is null)
        {
            return null;
        }

        var encryptedB = SelectEncryptedReplica(
            externalEncrypted,
            repoOwnerOrganizationId,
            request,
            excluded: [primaryAndRead.Id, encryptedA.Id]
        );
        if (encryptedB is null)
        {
            return null;
        }

        return new ReplicaSetSelection(primaryAndRead, primaryAndRead, encryptedA, encryptedB);
    }

    private static ReplicaSetSelection? SelectTier2(
        IReadOnlyList<StorageNodeDto> orgNodes,
        IReadOnlyList<StorageNodeDto> externalEncrypted,
        Guid repoOwnerOrganizationId,
        ReplicaSetPlannerRequest request
    )
    {
        if (orgNodes.Count < 2)
        {
            return null;
        }

        var primaryAndRead = orgNodes[0];
        var orgEncrypted = orgNodes[1];
        var external = SelectEncryptedReplica(
            externalEncrypted,
            repoOwnerOrganizationId,
            request,
            excluded: [primaryAndRead.Id, orgEncrypted.Id]
        );
        if (external is null)
        {
            return null;
        }

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

    private static StorageNodeDto? SelectEncryptedReplica(
        IReadOnlyList<StorageNodeDto> candidates,
        Guid? repoOwnerOrganizationId,
        ReplicaSetPlannerRequest request,
        IReadOnlyCollection<StorageNodeId> excluded
    ) =>
        EncryptedReplicaPlacementEngine.SelectBest(
            candidates,
            repoOwnerOrganizationId,
            request.RequiredBytesPerNode,
            excluded,
            request.RepositoryCountsByNodeId
        );

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
