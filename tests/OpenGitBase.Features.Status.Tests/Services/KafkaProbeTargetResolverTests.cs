using OpenGitBase.Features.Status.Services;

namespace OpenGitBase.Features.Status.Tests.Services;

public class KafkaProbeTargetResolverTests
{
    [Fact]
    public void Resolve_ParsesCommaSeparatedBootstrapServers()
    {
        var targets = KafkaProbeTargetResolver.Resolve(
            "kafka-1:29092,kafka-2:29092,kafka-3:29092"
        );

        Assert.Equal(3, targets.Count);
        Assert.Equal("kafka-1", targets[0].InstanceId);
        Assert.Equal("kafka-1", targets[0].Host);
        Assert.Equal(29092, targets[0].Port);
        Assert.Equal("kafka-3", targets[2].InstanceId);
    }

    [Fact]
    public void Resolve_DefaultsPortWhenMissing()
    {
        var targets = KafkaProbeTargetResolver.Resolve("broker.internal");

        var broker = Assert.Single(targets);
        Assert.Equal("broker.internal", broker.InstanceId);
        Assert.Equal(9092, broker.Port);
    }

    [Fact]
    public void Resolve_WhenMissingBootstrapServers_ReturnsEmpty()
    {
        var targets = KafkaProbeTargetResolver.Resolve(null);
        Assert.Empty(targets);
    }
}
