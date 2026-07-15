using OpenGitBase.Features.Status.Contracts;
using OpenGitBase.Features.Status.Services;

namespace OpenGitBase.Features.Status.Tests.Services;

public class MessageBusGroupStatusBuilderTests
{
    private readonly MessageBusGroupStatusBuilder _builder = new();

    [Theory]
    [InlineData(3, PublicHealthStatus.Healthy)]
    [InlineData(2, PublicHealthStatus.Degraded)]
    [InlineData(1, PublicHealthStatus.Unhealthy)]
    [InlineData(0, PublicHealthStatus.Unhealthy)]
    public void Build_WithThreeBrokers_AppliesQuorumThresholds(
        int healthyCount,
        PublicHealthStatus expected
    )
    {
        var instances = Enumerable
            .Range(1, 3)
            .Select(index =>
                CreateInstance(
                    $"kafka-{index}",
                    index <= healthyCount
                        ? PublicHealthStatus.Healthy
                        : PublicHealthStatus.Unhealthy
                )
            )
            .ToList();

        var group = _builder.Build(instances);

        Assert.Equal(StatusComponentGroup.MessageBus, group.Group);
        Assert.Equal(expected, group.Status);
        Assert.Equal(3, group.Instances.Count);
    }

    [Fact]
    public void Build_WithSingleBroker_UsesWorstChildRollup()
    {
        var instances = new[] { CreateInstance("kafka-1", PublicHealthStatus.Healthy) };

        var group = _builder.Build(instances);

        Assert.Equal(PublicHealthStatus.Healthy, group.Status);
    }

    [Fact]
    public void Build_WhenEmpty_ReturnsUnhealthy()
    {
        var group = _builder.Build(Array.Empty<StatusInstanceSnapshot>());

        Assert.Equal(PublicHealthStatus.Unhealthy, group.Status);
        Assert.Empty(group.Instances);
    }

    private static StatusInstanceSnapshot CreateInstance(
        string instanceId,
        PublicHealthStatus status
    ) =>
        new()
        {
            InstanceId = instanceId,
            Status = status,
            LastCheckedAt = DateTimeOffset.UtcNow,
        };
}
