using OpenGitBase.Features.Status.Contracts;
using OpenGitBase.Features.Status.Services;

namespace OpenGitBase.Features.Status.Tests.Services;

public class StatusRollupEngineTests
{
    [Theory]
    [InlineData(new[] { PublicHealthStatus.Healthy }, PublicHealthStatus.Healthy)]
    [InlineData(new[] { PublicHealthStatus.Healthy, PublicHealthStatus.Degraded }, PublicHealthStatus.Degraded)]
    [InlineData(new[] { PublicHealthStatus.Healthy, PublicHealthStatus.Unhealthy }, PublicHealthStatus.Unhealthy)]
    [InlineData(new[] { PublicHealthStatus.Degraded, PublicHealthStatus.Degraded }, PublicHealthStatus.Degraded)]
    [InlineData(new[] { PublicHealthStatus.Unhealthy, PublicHealthStatus.Degraded }, PublicHealthStatus.Unhealthy)]
    public void RollupGroup_UsesWorstChild(
        PublicHealthStatus[] statuses,
        PublicHealthStatus expected
    )
    {
        var result = StatusRollupEngine.RollupGroup(statuses);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void RollupGroup_WhenEmpty_ReturnsUnhealthy()
    {
        Assert.Equal(
            PublicHealthStatus.Unhealthy,
            StatusRollupEngine.RollupGroup(Array.Empty<PublicHealthStatus>())
        );
    }

    [Fact]
    public void RollupOverall_MatchesGroupRollup()
    {
        var groups = new[]
        {
            PublicHealthStatus.Healthy,
            PublicHealthStatus.Degraded,
            PublicHealthStatus.Healthy,
        };

        Assert.Equal(PublicHealthStatus.Degraded, StatusRollupEngine.RollupOverall(groups));
    }
}
