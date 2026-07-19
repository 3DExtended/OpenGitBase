using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Features.Status.Contracts;
using OpenGitBase.Features.Status.Entities;

namespace OpenGitBase.Features.Status.Services;

public sealed class StatusOutageWindowService
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly ISystemClock _clock;
    private readonly ILogger<StatusOutageWindowService> _logger;

    public StatusOutageWindowService(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        ISystemClock clock,
        ILogger<StatusOutageWindowService> logger
    )
    {
        _contextFactory = contextFactory;
        _clock = clock;
        _logger = logger;
    }

    public async Task ApplySnapshotAsync(
        PublicStatusSnapshotDto snapshot,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var existing = await context
            .Set<StatusOutageWindowEntity>()
            .Where(e => e.EndedAt == null)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var records = existing.Select(ToRecord).ToList();
        var observations = OutageHealthObservationBuilder.FromSnapshot(snapshot);
        var now = _clock.UtcNow;
        var result = OutageWindowDetector.Apply(records, observations, now);

        foreach (var id in result.Deletes)
        {
            var entity = existing.FirstOrDefault(e => e.Id == id);
            if (entity is not null)
            {
                context.Set<StatusOutageWindowEntity>().Remove(entity);
            }
        }

        foreach (var upsert in result.Upserts)
        {
            var entity = existing.FirstOrDefault(e => e.Id == upsert.Id);
            if (entity is null)
            {
                entity = new StatusOutageWindowEntity { Id = upsert.Id };
                context.Set<StatusOutageWindowEntity>().Add(entity);
                existing.Add(entity);
            }

            ApplyRecord(entity, upsert, now);
        }

        foreach (var log in result.Logs)
        {
            _logger.LogInformation("Status outage window: {Message}", log);
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        snapshot.OpenWindows = await ListOpenPublicWindowsAsync(context, now, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<List<PublicStatusOutageWindowDto>> ListOpenPublicWindowsAsync(
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        return await ListOpenPublicWindowsAsync(context, _clock.UtcNow, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task PruneOlderThanAsync(TimeSpan retention, CancellationToken cancellationToken)
    {
        var cutoff = _clock.UtcNow - retention;
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var stale = await context
            .Set<StatusOutageWindowEntity>()
            .Where(e =>
                (e.EndedAt != null && e.EndedAt < cutoff)
                || (e.EndedAt == null && e.BecamePublicAt == null && e.UnhealthySince < cutoff)
            )
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (stale.Count == 0)
        {
            return;
        }

        context.Set<StatusOutageWindowEntity>().RemoveRange(stale);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Pruned {Count} status outage windows older than {Cutoff}",
            stale.Count,
            cutoff
        );
    }

    private static async Task<List<PublicStatusOutageWindowDto>> ListOpenPublicWindowsAsync(
        OpenGitBaseDbContext context,
        DateTimeOffset now,
        CancellationToken cancellationToken
    )
    {
        var rows = await context
            .Set<StatusOutageWindowEntity>()
            .Where(e => e.EndedAt == null && e.BecamePublicAt != null && !e.Suppressed)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .OrderByDescending(e => e.UnhealthySince)
            .Select(e => ToDto(e, now))
            .ToList();
    }

    private static OutageWindowRecord ToRecord(StatusOutageWindowEntity entity) =>
        new()
        {
            Id = entity.Id,
            Scope = entity.Scope,
            Group = entity.ComponentGroup,
            InstanceId = entity.InstanceId,
            UnhealthySince = entity.UnhealthySince,
            BecamePublicAt = entity.BecamePublicAt,
            EndedAt = entity.EndedAt,
            LastNonUnhealthyAt = entity.LastNonUnhealthyAt,
            DisplayName = entity.DisplayName,
            IsPartial = entity.IsPartial,
            Suppressed = entity.Suppressed,
            Annotation = entity.Annotation,
        };

    private static void ApplyRecord(
        StatusOutageWindowEntity entity,
        OutageWindowRecord record,
        DateTimeOffset now
    )
    {
        entity.Scope = record.Scope;
        entity.ComponentGroup = record.Group;
        entity.InstanceId = record.InstanceId;
        entity.DisplayName = record.DisplayName;
        entity.UnhealthySince = record.UnhealthySince;
        entity.BecamePublicAt = record.BecamePublicAt;
        entity.EndedAt = record.EndedAt;
        entity.LastNonUnhealthyAt = record.LastNonUnhealthyAt;
        entity.IsPartial = record.IsPartial;
        entity.UpdatedAt = now;
        // Suppressed and Annotation are operator-owned; preserve existing values.
    }

    private static PublicStatusOutageWindowDto ToDto(
        StatusOutageWindowEntity entity,
        DateTimeOffset now
    )
    {
        var end = entity.EndedAt ?? now;
        var duration = (end - entity.UnhealthySince).TotalMinutes;
        return new PublicStatusOutageWindowDto
        {
            Id = entity.Id,
            Scope = entity.Scope,
            Group = entity.ComponentGroup,
            InstanceId = entity.InstanceId,
            DisplayName = entity.DisplayName,
            StartedAt = entity.UnhealthySince,
            EndedAt = entity.EndedAt,
            IsOpen = entity.EndedAt is null,
            IsPartial = entity.IsPartial,
            DurationMinutes = Math.Round(duration, 1),
            Annotation = entity.Annotation,
        };
    }
}
