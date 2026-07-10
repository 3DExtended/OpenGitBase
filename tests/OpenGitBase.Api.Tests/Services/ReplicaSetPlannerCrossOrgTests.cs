using OpenGitBase.Api.Services;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Tests.Services;

public class ReplicaSetPlannerCrossOrgTests
{
    [Fact]
    public void SelectReplicaSet_Tier0_PrefersCrossOrgEncryptedReplicas()
    {
        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();
        var platform1 = CreateNode("storage-1", 900, null);
        var platform2 = CreateNode("storage-2", 800, null);
        var platform3 = CreateNode("storage-3", 700, null);
        var crossOrg = CreateNode("org-b-node", 600, orgB, HostingScope.CrossOrgAllowed);

        var result = ReplicaSetPlanner.SelectReplicaSet(
            new ReplicaSetPlannerRequest(
                [platform1, platform2, platform3, crossOrg],
                OwnerOrganizationId: orgA,
                PlacementPolicy: PlacementPolicy.PlatformDefault,
                RequiredBytesPerNode: 100
            )
        );

        Assert.NotNull(result);
        Assert.Null(result!.Primary.OwnerOrganizationId);
        Assert.Null(result.ReadReplica.OwnerOrganizationId);
        Assert.Equal(orgB, result.EncryptedReplicaA.OwnerOrganizationId);
        Assert.Contains(
            result.EncryptedReplicaB.OwnerOrganizationId,
            new Guid?[] { orgB, null }
        );
    }

    [Fact]
    public void SelectReplicaSet_Tier1_PlacesEncryptedOnCrossOrgWhenAvailable()
    {
        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();
        var orgNode = CreateNode("org-a", 500, orgA);
        var crossOrg = CreateNode("org-b-node", 450, orgB, HostingScope.CrossOrgAllowed);
        var platformA = CreateNode("platform-a", 400, null);
        var platformB = CreateNode("platform-b", 300, null);

        var result = ReplicaSetPlanner.SelectReplicaSet(
            new ReplicaSetPlannerRequest(
                [orgNode, crossOrg, platformA, platformB],
                OwnerOrganizationId: orgA,
                PlacementPolicy: PlacementPolicy.MaxSelfHost,
                SelfHostPreference: SelfHostPreference.PreferSelfHost,
                RequiredBytesPerNode: 100
            )
        );

        Assert.NotNull(result);
        Assert.Equal("org-a", result!.Primary.NodeId);
        Assert.Equal(orgB, result.EncryptedReplicaA.OwnerOrganizationId);
        Assert.True(
            result.EncryptedReplicaB.OwnerOrganizationId == orgB
                || result.EncryptedReplicaB.OwnerOrganizationId is null
        );
    }

    [Fact]
    public void SelectReplicaSet_UserOwnedRepo_UsesCrossOrgEncryptedWhenAvailable()
    {
        var orgB = Guid.NewGuid();
        var platform1 = CreateNode("storage-1", 900, null);
        var platform2 = CreateNode("storage-2", 800, null);
        var platform3 = CreateNode("storage-3", 700, null);
        var crossOrg = CreateNode("org-b-node", 650, orgB, HostingScope.CrossOrgAllowed);

        var result = ReplicaSetPlanner.SelectReplicaSet(
            new ReplicaSetPlannerRequest(
                [platform1, platform2, platform3, crossOrg],
                RequiredBytesPerNode: 100
            )
        );

        Assert.NotNull(result);
        Assert.Equal(orgB, result!.EncryptedReplicaA.OwnerOrganizationId);
    }

    private static StorageNodeDto CreateNode(
        string nodeId,
        long freeBytes,
        Guid? ownerOrganizationId,
        HostingScope hostingScope = HostingScope.OwnOrgOnly
    ) =>
        new()
        {
            Id = StorageNodeId.From(Guid.NewGuid()),
            NodeId = nodeId,
            InternalHost = nodeId,
            InternalHttpPort = 8081,
            FreeBytesAvailable = freeBytes,
            OwnerOrganizationId = ownerOrganizationId,
            HostingScope = hostingScope,
            IsHealthy = true,
        };
}
