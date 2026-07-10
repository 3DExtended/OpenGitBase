using OpenGitBase.Api.Services;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Tests.Services;

public class ReplicaSetPlannerTests
{
    [Fact]
    public void SelectReplicaSet_WhenFewerThanThreeNodes_ReturnsNull()
    {
        var nodes = new[] { CreateNode("a", 100), CreateNode("b", 200) };

        var result = ReplicaSetPlanner.SelectReplicaSet(nodes);

        Assert.Null(result);
    }

    [Fact]
    public void SelectReplicaSet_PicksPrimaryAndTwoDistinctReplicasByFreeBytes()
    {
        var low = CreateNode("low", 100);
        var high = CreateNode("high", 500);
        var medium = CreateNode("medium", 250);
        var extra = CreateNode("extra", 50);

        var result = ReplicaSetPlanner.SelectReplicaSet([low, high, medium, extra]);

        Assert.NotNull(result);
        Assert.Equal("high", result!.Primary.NodeId);
        Assert.Equal("high", result.ReadReplica.NodeId);
        Assert.Equal("medium", result.EncryptedReplicaA.NodeId);
        Assert.Equal("low", result.EncryptedReplicaB.NodeId);
    }

    [Fact]
    public void SelectReplicaSet_WhenExactlyThreeNodes_ReturnsAllDistinct()
    {
        var nodes = new[] { CreateNode("a", 300), CreateNode("b", 200), CreateNode("c", 100) };

        var result = ReplicaSetPlanner.SelectReplicaSet(nodes);

        Assert.NotNull(result);
        var nodeIds = result!.AllNodes.Select(node => node.NodeId).ToList();
        Assert.Equal(3, nodeIds.Distinct().Count());
    }

    private static StorageNodeDto CreateNode(string nodeId, long freeBytes) =>
        new()
        {
            Id = StorageNodeId.From(Guid.NewGuid()),
            NodeId = nodeId,
            InternalHost = nodeId,
            InternalHttpPort = 8081,
            FreeBytesAvailable = freeBytes,
            IsHealthy = true,
        };
}
