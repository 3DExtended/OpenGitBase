using OpenGitBase.Features.Status.Contracts;
using OpenGitBase.Features.Status.Entities;
using OpenGitBase.Features.Status.Services;

namespace OpenGitBase.Features.Status.Tests.Services;

public class StatusHistoryDailyRollupTests
{
    [Fact]
    public void BuildDailySeries_UsesHalfWeightForDegradedUptime()
    {
        var day = new DateTimeOffset(2026, 7, 10, 12, 0, 0, TimeSpan.Zero);
        var buckets = new List<StatusHistoryHourlyBucketEntity>
        {
            CreateBucket(day, healthy: 1, degraded: 1, unhealthy: 0),
            CreateBucket(day.AddHours(1), healthy: 0, degraded: 0, unhealthy: 1),
        };

        var series = StatusHistoryDailyRollup.BuildDailySeries(buckets);

        var point = Assert.Single(series);
        Assert.Equal(DateOnly.FromDateTime(day.UtcDateTime), point.Date);
        Assert.Equal(50, point.UptimePercent);
        Assert.Equal(0.3333, point.HealthyRatio, 3);
        Assert.Equal(0.3333, point.DegradedRatio, 3);
        Assert.Equal(0.3333, point.UnhealthyRatio, 3);
    }

    [Fact]
    public void BuildDailySeries_GroupsMultipleHoursIntoSingleDay()
    {
        var day = new DateTimeOffset(2026, 7, 9, 10, 0, 0, TimeSpan.Zero);
        var buckets = new List<StatusHistoryHourlyBucketEntity>
        {
            CreateBucket(day, healthy: 2, degraded: 0, unhealthy: 0),
            CreateBucket(day.AddHours(2), healthy: 2, degraded: 0, unhealthy: 0),
        };

        var series = StatusHistoryDailyRollup.BuildDailySeries(buckets);

        var point = Assert.Single(series);
        Assert.Equal(100, point.UptimePercent);
    }

    private static StatusHistoryHourlyBucketEntity CreateBucket(
        DateTimeOffset periodStart,
        int healthy,
        int degraded,
        int unhealthy
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            ComponentGroup = StatusComponentGroup.Overall,
            PeriodStartUtc = periodStart,
            HealthySamples = healthy,
            DegradedSamples = degraded,
            UnhealthySamples = unhealthy,
            TotalSamples = healthy + degraded + unhealthy,
        };
}
