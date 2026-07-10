using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Features.Status.Contracts;
using OpenGitBase.Features.Status.Entities;

namespace OpenGitBase.Features.Status.Services;

public sealed class StatusHistoryService
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly StatusProbeOptions _options;

    public StatusHistoryService(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        StatusProbeOptions options
    )
    {
        _contextFactory = contextFactory;
        _options = options;
    }

    public async Task RecordSnapshotAsync(
        PublicStatusSnapshotDto snapshot,
        CancellationToken cancellationToken
    )
    {
        var periodStart = TruncateToHour(snapshot.CheckedAt);
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        await UpsertBucketAsync(
            context,
            StatusComponentGroup.Overall,
            periodStart,
            snapshot.OverallStatus,
            cancellationToken
        ).ConfigureAwait(false);

        foreach (var group in snapshot.Groups)
        {
            await UpsertBucketAsync(
                context,
                group.Group,
                periodStart,
                group.Status,
                cancellationToken
            ).ConfigureAwait(false);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await PruneOldBucketsAsync(context, cancellationToken).ConfigureAwait(false);
    }

    public async Task<PublicStatusHistoryDto> GetHistoryAsync(
        int days,
        CancellationToken cancellationToken
    )
    {
        var clampedDays = Math.Clamp(days, 1, _options.HistoryRetentionDays);
        var cutoff = TruncateToHour(DateTimeOffset.UtcNow.AddDays(-clampedDays));

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var buckets = await context
            .Set<StatusHistoryHourlyBucketEntity>()
            .AsNoTracking()
            .Where(entity => entity.PeriodStartUtc >= cutoff)
            .OrderBy(entity => entity.PeriodStartUtc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var groupSeries = Enum.GetValues<StatusComponentGroup>()
            .Where(group => group != StatusComponentGroup.Overall)
            .Select(group => new PublicStatusHistoryGroupSeriesDto
            {
                Group = group,
                Days = StatusHistoryDailyRollup.BuildDailySeries(
                    buckets.Where(bucket => bucket.ComponentGroup == group).ToList()
                ),
            })
            .ToList();

        var overallBuckets = buckets
            .Where(bucket => bucket.ComponentGroup == StatusComponentGroup.Overall)
            .ToList();

        return new PublicStatusHistoryDto
        {
            Groups = groupSeries,
            Overall = StatusHistoryDailyRollup.BuildDailySeries(overallBuckets),
            OverallStateMix = StatusHistoryDailyRollup.BuildDailySeries(overallBuckets),
        };
    }

    private static DateTimeOffset TruncateToHour(DateTimeOffset value) =>
        new(
            value.UtcDateTime.Date.AddHours(value.UtcDateTime.Hour),
            TimeSpan.Zero
        );

    private static async Task UpsertBucketAsync(
        OpenGitBaseDbContext context,
        StatusComponentGroup group,
        DateTimeOffset periodStart,
        PublicHealthStatus status,
        CancellationToken cancellationToken
    )
    {
        var bucket = await context
            .Set<StatusHistoryHourlyBucketEntity>()
            .FirstOrDefaultAsync(
                entity =>
                    entity.ComponentGroup == group && entity.PeriodStartUtc == periodStart,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (bucket is null)
        {
            bucket = new StatusHistoryHourlyBucketEntity
            {
                Id = Guid.NewGuid(),
                ComponentGroup = group,
                PeriodStartUtc = periodStart,
            };
            context.Set<StatusHistoryHourlyBucketEntity>().Add(bucket);
        }

        switch (status)
        {
            case PublicHealthStatus.Healthy:
                bucket.HealthySamples += 1;
                break;
            case PublicHealthStatus.Degraded:
                bucket.DegradedSamples += 1;
                break;
            default:
                bucket.UnhealthySamples += 1;
                break;
        }

        bucket.TotalSamples += 1;
    }

    private async Task PruneOldBucketsAsync(
        OpenGitBaseDbContext context,
        CancellationToken cancellationToken
    )
    {
        var cutoff = TruncateToHour(
            DateTimeOffset.UtcNow.AddDays(-_options.HistoryRetentionDays)
        );
        var stale = await context
            .Set<StatusHistoryHourlyBucketEntity>()
            .Where(entity => entity.PeriodStartUtc < cutoff)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (stale.Count == 0)
        {
            return;
        }

        context.Set<StatusHistoryHourlyBucketEntity>().RemoveRange(stale);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
