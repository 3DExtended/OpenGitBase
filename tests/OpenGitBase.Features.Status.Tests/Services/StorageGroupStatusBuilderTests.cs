using OpenGitBase.Common.Options;
using OpenGitBase.Features.Status.Contracts;
using OpenGitBase.Features.Status.Services;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Features.Status.Tests.Services;

public class StorageGroupStatusBuilderTests
{
    private readonly StorageGroupStatusBuilder _builder = new(
        new StorageNodeOptions { MissedHeartbeatThresholdSeconds = 90 }
    );

    [Theory]
    [InlineData(3, PublicHealthStatus.Healthy)]
    [InlineData(2, PublicHealthStatus.Degraded)]
    [InlineData(1, PublicHealthStatus.Unhealthy)]
    [InlineData(0, PublicHealthStatus.Unhealthy)]
    public void Build_AppliesHealthyCountThresholds(int healthyCount, PublicHealthStatus expected)
    {
        var checkedAt = DateTimeOffset.UtcNow;
        var nodes = Enumerable
            .Range(1, 3)
            .Select(index =>
                CreateNode(
                    $"storage-{index}",
                    isHealthy: index <= healthyCount,
                    lastHeartbeatAt: checkedAt.AddSeconds(-30)
                )
            )
            .ToList();

        var group = _builder.Build(nodes, checkedAt);

        Assert.Equal(StatusComponentGroup.Storage, group.Group);
        Assert.Equal(expected, group.Status);
        Assert.Equal(3, group.Instances.Count);
    }

    [Fact]
    public void Build_StaleHeartbeat_MarksInstanceDegraded()
    {
        var checkedAt = DateTimeOffset.UtcNow;
        var nodes = new[]
        {
            CreateNode("storage-1", true, checkedAt.AddSeconds(-30)),
            CreateNode("storage-2", true, checkedAt.AddMinutes(-5)),
            CreateNode("storage-3", true, checkedAt.AddSeconds(-20)),
        };

        var group = _builder.Build(nodes, checkedAt);
        var stale = group.Instances.Single(instance => instance.InstanceId == "storage-2");

        Assert.Equal(PublicHealthStatus.Degraded, stale.Status);
        Assert.Equal("Heartbeat stale", stale.Message);
    }

    [Fact]
    public void Build_UnhealthyNode_MarksInstanceUnhealthy()
    {
        var checkedAt = DateTimeOffset.UtcNow;
        var nodes = new[]
        {
            CreateNode("storage-1", false, checkedAt.AddSeconds(-10)),
            CreateNode("storage-2", true, checkedAt.AddSeconds(-10)),
            CreateNode("storage-3", true, checkedAt.AddSeconds(-10)),
        };

        var group = _builder.Build(nodes, checkedAt);
        var unhealthy = group.Instances.Single(instance => instance.InstanceId == "storage-1");

        Assert.Equal(PublicHealthStatus.Unhealthy, unhealthy.Status);
    }

    private static StorageNodeDto CreateNode(
        string nodeId,
        bool isHealthy,
        DateTimeOffset? lastHeartbeatAt
    ) =>
        new()
        {
            NodeId = nodeId,
            IsHealthy = isHealthy,
            LastHeartbeatAt = lastHeartbeatAt,
            RegisteredAt = DateTimeOffset.UtcNow.AddDays(-1),
        };
}
