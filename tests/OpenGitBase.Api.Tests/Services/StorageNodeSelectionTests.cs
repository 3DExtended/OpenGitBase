using OpenGitBase.Api.Services;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Tests.Services;

public class StorageNodeSelectionTests
{
    [Fact]
    public void SelectBestNode_WhenEmpty_ReturnsNull()
    {
        var result = StorageNodeSelection.SelectBestNode(Array.Empty<StorageNodeDto>());
        Assert.Null(result);
    }

    [Fact]
    public void SelectBestNode_PicksHighestFreeBytes()
    {
        var low = CreateNode("low", 100);
        var high = CreateNode("high", 500);
        var medium = CreateNode("medium", 250);

        var result = StorageNodeSelection.SelectBestNode([low, medium, high]);

        Assert.NotNull(result);
        Assert.Equal("high", result!.NodeId);
    }

    [Fact]
    public void SelectBestNode_WhenTieBreaksByNodeId()
    {
        var nodeB = CreateNode("b-node", 500);
        var nodeA = CreateNode("a-node", 500);

        var result = StorageNodeSelection.SelectBestNode([nodeB, nodeA]);

        Assert.NotNull(result);
        Assert.Equal("a-node", result!.NodeId);
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
