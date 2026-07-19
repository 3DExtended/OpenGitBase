using OpenGitBase.Features.Status.Contracts;

namespace OpenGitBase.Features.Status.Services;

public static class OutageWindowDetector
{
    public static readonly TimeSpan MinUnhealthyDuration = TimeSpan.FromMinutes(5);

    public static readonly TimeSpan HealthyGapMerge = TimeSpan.FromMinutes(2);

    public static OutageWindowDetectorResult Apply(
        IReadOnlyList<OutageWindowRecord> existingOpenOrTracking,
        IReadOnlyList<OutageHealthObservation> observations,
        DateTimeOffset now
    )
    {
        var result = new OutageWindowDetectorResult();
        var active = existingOpenOrTracking
            .Where(w => w.EndedAt is null)
            .ToDictionary(KeyOf);

        var seenKeys = new HashSet<string>(StringComparer.Ordinal);

        foreach (var observation in observations)
        {
            if (!ShouldTrack(observation))
            {
                continue;
            }

            var key = KeyOf(observation);
            seenKeys.Add(key);
            active.TryGetValue(key, out var current);

            if (observation.Status == PublicHealthStatus.Unhealthy)
            {
                HandleUnhealthy(observation, current, now, active, result);
            }
            else
            {
                HandleNonUnhealthy(current, now, active, result);
            }
        }

        foreach (var (key, current) in active.ToList())
        {
            if (seenKeys.Contains(key))
            {
                continue;
            }

            HandleNonUnhealthy(current, now, active, result);
        }

        return result;
    }

    public static string ResolveDisplayName(OutageHealthObservation observation)
    {
        if (observation.Group == StatusComponentGroup.DataStores
            && !string.IsNullOrWhiteSpace(observation.InstanceId))
        {
            return observation.InstanceId.ToLowerInvariant() switch
            {
                "postgres" => "Postgres",
                "redis" => "Redis",
                _ => observation.InstanceId,
            };
        }

        if (observation.Scope == OutageWindowScope.Instance
            && !string.IsNullOrWhiteSpace(observation.InstanceId))
        {
            return observation.InstanceId;
        }

        return observation.Group switch
        {
            StatusComponentGroup.Website => "Website",
            StatusComponentGroup.Api => "API",
            StatusComponentGroup.Git => "Git",
            StatusComponentGroup.Storage => "Storage",
            StatusComponentGroup.DataStores => "Data stores",
            StatusComponentGroup.MessageBus => "Message bus",
            _ => observation.Group.ToString(),
        };
    }

    private static bool ShouldTrack(OutageHealthObservation observation)
    {
        if (observation.Group == StatusComponentGroup.Overall)
        {
            return false;
        }

        if (observation.Group == StatusComponentGroup.DataStores)
        {
            return observation.Scope == OutageWindowScope.Instance
                && !string.IsNullOrWhiteSpace(observation.InstanceId);
        }

        if (observation.Scope == OutageWindowScope.Group)
        {
            return observation.Group
                is StatusComponentGroup.Website
                    or StatusComponentGroup.Api
                    or StatusComponentGroup.Git
                    or StatusComponentGroup.Storage
                    or StatusComponentGroup.MessageBus;
        }

        return observation.Scope == OutageWindowScope.Instance
            && observation.Status == PublicHealthStatus.Unhealthy
            && observation.GroupStatus != PublicHealthStatus.Unhealthy;
    }

    private static void HandleUnhealthy(
        OutageHealthObservation observation,
        OutageWindowRecord? current,
        DateTimeOffset now,
        Dictionary<string, OutageWindowRecord> active,
        OutageWindowDetectorResult result
    )
    {
        if (current is null)
        {
            var created = NewTracking(observation, now);
            active[KeyOf(created)] = created;
            result.Upserts.Add(Clone(created));
            result.Logs.Add($"tracking started for {created.DisplayName}");
            return;
        }

        if (current.LastNonUnhealthyAt is { } gapStart)
        {
            var gap = now - gapStart;
            if (gap <= HealthyGapMerge)
            {
                current.LastNonUnhealthyAt = null;
                result.Logs.Add($"merged healthy gap for {current.DisplayName}");
            }
            else
            {
                current.LastNonUnhealthyAt = null;
                current.UnhealthySince = now;
                current.BecamePublicAt = null;
            }
        }

        if (current.BecamePublicAt is null
            && now - current.UnhealthySince >= MinUnhealthyDuration)
        {
            current.BecamePublicAt = now;
            result.Logs.Add($"opened outage window for {current.DisplayName}");
        }

        result.Upserts.Add(Clone(current));
    }

    private static void HandleNonUnhealthy(
        OutageWindowRecord? current,
        DateTimeOffset now,
        Dictionary<string, OutageWindowRecord> active,
        OutageWindowDetectorResult result
    )
    {
        if (current is null)
        {
            return;
        }

        current.LastNonUnhealthyAt ??= now;
        var gap = now - current.LastNonUnhealthyAt.Value;

        if (gap <= HealthyGapMerge)
        {
            result.Upserts.Add(Clone(current));
            return;
        }

        if (current.BecamePublicAt is null)
        {
            active.Remove(KeyOf(current));
            result.Deletes.Add(current.Id);
            result.Logs.Add($"discarded provisional window for {current.DisplayName}");
            return;
        }

        current.EndedAt = now;
        active.Remove(KeyOf(current));
        result.Upserts.Add(Clone(current));
        result.Logs.Add($"closed outage window for {current.DisplayName}");
    }

    private static OutageWindowRecord NewTracking(
        OutageHealthObservation observation,
        DateTimeOffset now
    )
    {
        var isPartial =
            observation.Scope == OutageWindowScope.Instance
            && observation.GroupStatus != PublicHealthStatus.Unhealthy
            && observation.Group != StatusComponentGroup.DataStores;

        return new OutageWindowRecord
        {
            Id = Guid.NewGuid(),
            Scope = observation.Scope,
            Group = observation.Group,
            InstanceId = observation.InstanceId,
            UnhealthySince = now,
            BecamePublicAt = null,
            EndedAt = null,
            LastNonUnhealthyAt = null,
            DisplayName = ResolveDisplayName(observation),
            IsPartial = isPartial,
        };
    }

    private static string KeyOf(OutageWindowRecord record) =>
        $"{(int)record.Scope}:{(int)record.Group}:{record.InstanceId ?? string.Empty}";

    private static string KeyOf(OutageHealthObservation observation) =>
        $"{(int)observation.Scope}:{(int)observation.Group}:{observation.InstanceId ?? string.Empty}";

    private static OutageWindowRecord Clone(OutageWindowRecord source) =>
        new()
        {
            Id = source.Id,
            Scope = source.Scope,
            Group = source.Group,
            InstanceId = source.InstanceId,
            UnhealthySince = source.UnhealthySince,
            BecamePublicAt = source.BecamePublicAt,
            EndedAt = source.EndedAt,
            LastNonUnhealthyAt = source.LastNonUnhealthyAt,
            DisplayName = source.DisplayName,
            IsPartial = source.IsPartial,
            Suppressed = source.Suppressed,
            Annotation = source.Annotation,
        };
}
