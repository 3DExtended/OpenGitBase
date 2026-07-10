using OpenGitBase.Api.Services;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Tests.Services;

public class EncryptedReplicaPlacementEngineTests
{
    [Fact]
    public void SelectBest_PrefersCrossOrgOverPlatform()
    {
        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();
        var platform = CreateNode("platform", 900, null);
        var crossOrg = CreateNode("cross-org", 500, orgB, HostingScope.CrossOrgAllowed);

        var selected = EncryptedReplicaPlacementEngine.SelectBest(
            [platform, crossOrg],
            repoOwnerOrganizationId: orgA,
            repoMaxBytes: 100,
            excluded: [],
            repositoryCounts: null
        );

        Assert.Equal("cross-org", selected!.NodeId);
    }

    [Fact]
    public void SelectBest_FallsBackToPlatformWhenNoCrossOrg()
    {
        var orgA = Guid.NewGuid();
        var platform = CreateNode("platform", 900, null);

        var selected = EncryptedReplicaPlacementEngine.SelectBest(
            [platform],
            repoOwnerOrganizationId: orgA,
            repoMaxBytes: 100,
            excluded: [],
            repositoryCounts: null
        );

        Assert.Equal("platform", selected!.NodeId);
    }

    [Fact]
    public void SelectBest_SkipsNodeWithoutCapacity()
    {
        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();
        var crossOrg = CreateNode("cross-org", 500, orgB, HostingScope.CrossOrgAllowed, maxBytes: 100, usedBytes: 95);
        var platform = CreateNode("platform", 900, null);

        var selected = EncryptedReplicaPlacementEngine.SelectBest(
            [crossOrg, platform],
            repoOwnerOrganizationId: orgA,
            repoMaxBytes: 100,
            excluded: [],
            repositoryCounts: null
        );

        Assert.Equal("platform", selected!.NodeId);
    }

    [Fact]
    public void SelectBest_ExcludesSameOrgOwnOrgOnlyNodes()
    {
        var orgA = Guid.NewGuid();
        var ownOrg = CreateNode("own-org", 900, orgA, HostingScope.OwnOrgOnly);
        var platform = CreateNode("platform", 400, null);

        var selected = EncryptedReplicaPlacementEngine.SelectBest(
            [ownOrg, platform],
            repoOwnerOrganizationId: orgA,
            repoMaxBytes: 100,
            excluded: [],
            repositoryCounts: null
        );

        Assert.Equal("platform", selected!.NodeId);
    }

    [Fact]
    public void ScoreNode_PrefersLowerRepositoryCountAtEqualFreeBytes()
    {
        var orgA = Guid.NewGuid();
        var orgB = Guid.NewGuid();
        var busy = CreateNode("busy", 500, orgB, HostingScope.CrossOrgAllowed);
        var idle = CreateNode("idle", 500, orgB, HostingScope.CrossOrgAllowed);
        var counts = new Dictionary<Guid, int>
        {
            [busy.Id.Value] = 50,
            [idle.Id.Value] = 2,
        };

        var busyScore = EncryptedReplicaPlacementEngine.ScoreNode(busy, orgA, 100, counts[busy.Id.Value]);
        var idleScore = EncryptedReplicaPlacementEngine.ScoreNode(idle, orgA, 100, counts[idle.Id.Value]);

        Assert.True(idleScore > busyScore);
    }

    private static StorageNodeDto CreateNode(
        string nodeId,
        long freeBytes,
        Guid? ownerOrganizationId,
        HostingScope hostingScope = HostingScope.OwnOrgOnly,
        long maxBytes = 0,
        long usedBytes = 0
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
            MaxBytes = maxBytes,
            UsedBytes = usedBytes,
            IsHealthy = true,
        };
}
