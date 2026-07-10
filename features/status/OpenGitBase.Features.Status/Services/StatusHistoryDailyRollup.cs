using OpenGitBase.Features.Status.Contracts;
using OpenGitBase.Features.Status.Entities;

namespace OpenGitBase.Features.Status.Services;

public static class StatusHistoryDailyRollup
{
    private const double DegradedUptimeWeight = 0.5;

    public static List<PublicStatusHistoryDayDto> BuildDailySeries(
        IReadOnlyList<StatusHistoryHourlyBucketEntity> buckets
    )
    {
        return buckets
            .GroupBy(bucket => DateOnly.FromDateTime(bucket.PeriodStartUtc.UtcDateTime))
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var healthy = group.Sum(bucket => bucket.HealthySamples);
                var degraded = group.Sum(bucket => bucket.DegradedSamples);
                var unhealthy = group.Sum(bucket => bucket.UnhealthySamples);
                var total = healthy + degraded + unhealthy;
                var uptimePercent = total == 0
                    ? 0
                    : (healthy + DegradedUptimeWeight * degraded) / total * 100;

                return new PublicStatusHistoryDayDto
                {
                    Date = group.Key,
                    UptimePercent = Math.Round(uptimePercent, 2),
                    HealthyRatio = total == 0 ? 0 : Math.Round((double)healthy / total, 4),
                    DegradedRatio = total == 0 ? 0 : Math.Round((double)degraded / total, 4),
                    UnhealthyRatio = total == 0 ? 0 : Math.Round((double)unhealthy / total, 4),
                };
            })
            .ToList();
    }
}
