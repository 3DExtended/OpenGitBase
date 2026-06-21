using OpenGitBase.Api.Services;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Api.Tests.Services;

public class ReplicationAttentionTests
{
    [Fact]
    public void NeedsAttention_WhenRf3HealthyInSyncWithQuorum_ReturnsFalse()
    {
        Assert.False(ReplicationAttention.NeedsAttention(HealthySummary()));
    }

    [Fact]
    public void NeedsAttention_WhenBackfilling_ReturnsTrue()
    {
        var summary = SummaryFrom(nameof(ReplicationState.Rf1Backfilling), 3, 0, true);

        Assert.True(ReplicationAttention.NeedsAttention(summary));
    }

    [Fact]
    public void NeedsAttention_WhenHealthyButLagging_ReturnsTrue()
    {
        var summary = SummaryFrom(nameof(ReplicationState.Rf3Healthy), 3, 2, true);

        Assert.True(ReplicationAttention.NeedsAttention(summary));
    }

    [Fact]
    public void NeedsAttention_WhenNoWriteQuorum_ReturnsTrue()
    {
        var summary = SummaryFrom(nameof(ReplicationState.Rf3Healthy), 3, 0, false);

        Assert.True(ReplicationAttention.NeedsAttention(summary));
    }

    [Fact]
    public void MatchesPreset_Backfilling_FiltersByState()
    {
        var backfilling = SummaryFrom(nameof(ReplicationState.Rf1Backfilling), 2, 0, true);

        Assert.True(
            ReplicationAttention.MatchesPreset(
                backfilling,
                ReplicationAttentionPreset.Backfilling
            )
        );
        Assert.False(
            ReplicationAttention.MatchesPreset(
                HealthySummary(),
                ReplicationAttentionPreset.Backfilling
            )
        );
    }

    [Fact]
    public void CompareSeverity_NoQuorumRanksBeforeHealthy()
    {
        var noQuorum = SummaryFrom(nameof(ReplicationState.Rf3Healthy), 3, 0, false);

        Assert.True(
            ReplicationAttention.CompareSeverity(noQuorum, HealthySummary()) < 0
        );
    }

    private static AdminRepositoryReplicationSummaryDto SummaryFrom(
        string state,
        int replicaCount,
        long maxLag,
        bool writeQuorum
    ) =>
        new()
        {
            RepositoryId = Guid.NewGuid(),
            Name = "repo",
            OwnerSlug = "owner",
            ReplicationState = state,
            ReplicaCount = replicaCount,
            PrimaryNodeId = "storage-1",
            PrimaryWatermark = 5,
            MaxWatermarkLag = maxLag,
            WriteQuorumAvailable = writeQuorum,
            ReplicationEpoch = 1,
        };

    private static AdminRepositoryReplicationSummaryDto HealthySummary() =>
        SummaryFrom(nameof(ReplicationState.Rf3Healthy), 3, 0, true);
}
