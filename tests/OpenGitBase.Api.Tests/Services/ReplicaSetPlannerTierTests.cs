using OpenGitBase.Api.Services;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Tests.Services;

public class ReplicaSetPlannerTierTests
{
    [Fact]
    public void SelectReplicaSet_Tier1_PlacesPrimaryOnOrgNode()
    {
        var orgId = Guid.NewGuid();
        var orgNode = CreateNode("org-1", 500, orgId);
        var platformA = CreateNode("platform-a", 400, null);
        var platformB = CreateNode("platform-b", 300, null);
        var platformC = CreateNode("platform-c", 200, null);

        var result = ReplicaSetPlanner.SelectReplicaSet(
            new ReplicaSetPlannerRequest(
                [orgNode, platformA, platformB, platformC],
                OwnerOrganizationId: orgId,
                PlacementPolicy: PlacementPolicy.MaxSelfHost,
                SelfHostPreference: SelfHostPreference.PreferSelfHost
            )
        );

        Assert.NotNull(result);
        Assert.Equal("org-1", result!.Primary.NodeId);
        Assert.Equal("org-1", result.ReadReplica.NodeId);
        Assert.DoesNotContain(
            result.AllNodes,
            node => node.OwnerOrganizationId is not null && node.OwnerOrganizationId != orgId
                && node.Id != result.EncryptedReplicaA.Id
                && node.Id != result.EncryptedReplicaB.Id
        );
    }

    [Fact]
    public void SelectReplicaSet_Tier3_UsesOnlyOrgNodes()
    {
        var orgId = Guid.NewGuid();
        var orgA = CreateNode("org-a", 500, orgId);
        var orgB = CreateNode("org-b", 400, orgId);
        var orgC = CreateNode("org-c", 300, orgId);
        var platform = CreateNode("platform", 900, null);

        var result = ReplicaSetPlanner.SelectReplicaSet(
            new ReplicaSetPlannerRequest(
                [orgA, orgB, orgC, platform],
                OwnerOrganizationId: orgId,
                PlacementPolicy: PlacementPolicy.MaxSelfHost,
                SelfHostPreference: SelfHostPreference.RequireSelfHost
            )
        );

        Assert.NotNull(result);
        Assert.True(result!.AllNodes.All(node => node.OwnerOrganizationId == orgId));
    }

    [Fact]
    public void SelectReplicaSet_RequireSelfHostWithoutOrgNodes_ReturnsNull()
    {
        var orgId = Guid.NewGuid();
        var platformNodes = Enumerable
            .Range(1, 3)
            .Select(index => CreateNode($"platform-{index}", 1000 - index, null))
            .ToList();

        var result = ReplicaSetPlanner.SelectReplicaSet(
            new ReplicaSetPlannerRequest(
                platformNodes,
                OwnerOrganizationId: orgId,
                SelfHostPreference: SelfHostPreference.RequireSelfHost
            )
        );

        Assert.Null(result);
    }

    [Fact]
    public void SelectReplicaSet_CrossOrgNodeNeverReceivesPrimaryOrRead()
    {
        var orgId = Guid.NewGuid();
        var otherOrgId = Guid.NewGuid();
        var orgNode = CreateNode("org-1", 500, orgId);
        var crossOrg = CreateNode("cross-org", 450, otherOrgId, HostingScope.CrossOrgAllowed);
        var platformA = CreateNode("platform-a", 400, null);
        var platformB = CreateNode("platform-b", 300, null);

        var result = ReplicaSetPlanner.SelectReplicaSet(
            new ReplicaSetPlannerRequest(
                [orgNode, crossOrg, platformA, platformB],
                OwnerOrganizationId: orgId,
                PlacementPolicy: PlacementPolicy.MaxSelfHost,
                SelfHostPreference: SelfHostPreference.PreferSelfHost
            )
        );

        Assert.NotNull(result);
        Assert.Equal("org-1", result!.Primary.NodeId);
        Assert.NotEqual(crossOrg.Id, result.ReadReplica.Id);
    }

    [Theory]
    [InlineData(0, SelfHostPreference.PlatformOnly, 0)]
    [InlineData(2, SelfHostPreference.PreferSelfHost, 2)]
    [InlineData(3, SelfHostPreference.RequireSelfHost, 3)]
    public void ResolveTier_MatchesExpected(
        int orgNodeCount,
        SelfHostPreference preference,
        int expectedTier
    )
    {
        var tier = ReplicaSetPlanner.ResolveTier(
            new ReplicaSetPlannerRequest(
                HealthyNodes: [],
                OwnerOrganizationId: Guid.NewGuid(),
                SelfHostPreference: preference
            ),
            orgNodeCount
        );

        Assert.Equal(expectedTier, tier);
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
