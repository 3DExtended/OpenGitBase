using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Api.Services;

public static class ReplicationAttention
{
    public static bool NeedsAttention(AdminRepositoryReplicationSummaryDto summary) =>
        !string.Equals(summary.ReplicationState, nameof(ReplicationState.Rf3Healthy), StringComparison.Ordinal)
        || !summary.WriteQuorumAvailable
        || summary.MaxWatermarkLag > 0
        || summary.ReplicaCount < 3;

    public static bool MatchesPreset(
        AdminRepositoryReplicationSummaryDto summary,
        ReplicationAttentionPreset preset
    ) =>
        preset switch
        {
            ReplicationAttentionPreset.All => true,
            ReplicationAttentionPreset.Backfilling =>
                string.Equals(
                    summary.ReplicationState,
                    nameof(ReplicationState.Rf1Backfilling),
                    StringComparison.Ordinal
                ),
            ReplicationAttentionPreset.Degraded =>
                string.Equals(summary.ReplicationState, nameof(ReplicationState.Degraded), StringComparison.Ordinal)
                || string.Equals(
                    summary.ReplicationState,
                    nameof(ReplicationState.Promoting),
                    StringComparison.Ordinal
                ),
            ReplicationAttentionPreset.Lagging => summary.MaxWatermarkLag > 0,
            ReplicationAttentionPreset.NoQuorum => !summary.WriteQuorumAvailable,
            _ => true,
        };

    public static int GetSeverityRank(AdminRepositoryReplicationSummaryDto summary)
    {
        if (!summary.WriteQuorumAvailable)
        {
            return 0;
        }

        if (
            string.Equals(summary.ReplicationState, nameof(ReplicationState.Degraded), StringComparison.Ordinal)
            || string.Equals(summary.ReplicationState, nameof(ReplicationState.Promoting), StringComparison.Ordinal)
        )
        {
            return 1;
        }

        if (
            string.Equals(
                summary.ReplicationState,
                nameof(ReplicationState.Rf1Backfilling),
                StringComparison.Ordinal
            )
        )
        {
            return 2;
        }

        if (summary.MaxWatermarkLag > 0 || summary.ReplicaCount < 3)
        {
            return 3;
        }

        return 4;
    }

    public static int CompareSeverity(
        AdminRepositoryReplicationSummaryDto left,
        AdminRepositoryReplicationSummaryDto right
    )
    {
        var rank = GetSeverityRank(left).CompareTo(GetSeverityRank(right));
        if (rank != 0)
        {
            return rank;
        }

        return string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
    }

    public static ReplicationAttentionPreset ParsePreset(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "backfilling" => ReplicationAttentionPreset.Backfilling,
            "degraded" => ReplicationAttentionPreset.Degraded,
            "lagging" => ReplicationAttentionPreset.Lagging,
            "no-quorum" => ReplicationAttentionPreset.NoQuorum,
            _ => ReplicationAttentionPreset.All,
        };

    public static AdminRepositoryReplicationSort ParseSort(string? value) =>
        value?.Trim().ToLowerInvariant() switch
        {
            "name" => AdminRepositoryReplicationSort.Name,
            "lag" => AdminRepositoryReplicationSort.Lag,
            "state" => AdminRepositoryReplicationSort.State,
            _ => AdminRepositoryReplicationSort.Severity,
        };
}
