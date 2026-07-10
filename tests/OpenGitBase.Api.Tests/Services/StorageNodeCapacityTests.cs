using OpenGitBase.Api.Services;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Tests.Services;

public class StorageNodeCapacityTests
{
    [Fact]
    public void HasCapacity_WhenMaxBytesZero_AlwaysTrue()
    {
        var node = CreateNode(maxBytes: 0, usedBytes: 1000);

        Assert.True(StorageNodeCapacity.HasCapacity(node, 500));
    }

    [Fact]
    public void HasCapacity_WhenAdditionalBytesExceedMax_ReturnsFalse()
    {
        var node = CreateNode(maxBytes: 1000, usedBytes: 900);

        Assert.False(StorageNodeCapacity.HasCapacity(node, 200));
    }

    [Fact]
    public void RemainingBytes_ReturnsDifference()
    {
        var node = CreateNode(maxBytes: 1000, usedBytes: 250);

        Assert.Equal(750, StorageNodeCapacity.RemainingBytes(node));
    }

    private static StorageNodeDto CreateNode(long maxBytes, long usedBytes) =>
        new()
        {
            Id = StorageNodeId.From(Guid.NewGuid()),
            NodeId = "node-a",
            InternalHost = "node-a",
            InternalHttpPort = 8081,
            MaxBytes = maxBytes,
            UsedBytes = usedBytes,
            IsHealthy = true,
        };
}
