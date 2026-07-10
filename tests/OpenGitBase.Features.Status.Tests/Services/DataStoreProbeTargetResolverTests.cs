using OpenGitBase.Features.Status.Services;

namespace OpenGitBase.Features.Status.Tests.Services;

public class DataStoreProbeTargetResolverTests
{
    [Fact]
    public void Resolve_ParsesPostgresHostAndPort()
    {
        var targets = DataStoreProbeTargetResolver.Resolve(
            "Host=db.internal;Port=5433;Database=opengitbase",
            null
        );

        var postgres = Assert.Single(targets);
        Assert.Equal("postgres", postgres.InstanceId);
        Assert.Equal("db.internal", postgres.Host);
        Assert.Equal(5433, postgres.Port);
    }

    [Fact]
    public void Resolve_ParsesRedisUrl()
    {
        var targets = DataStoreProbeTargetResolver.Resolve(
            null,
            "redis://cache.internal:6380"
        );

        var redis = Assert.Single(targets);
        Assert.Equal("redis", redis.InstanceId);
        Assert.Equal("cache.internal", redis.Host);
        Assert.Equal(6380, redis.Port);
    }

    [Fact]
    public void Resolve_WhenMissingConnectionStrings_ReturnsEmpty()
    {
        var targets = DataStoreProbeTargetResolver.Resolve(null, null);
        Assert.Empty(targets);
    }
}
