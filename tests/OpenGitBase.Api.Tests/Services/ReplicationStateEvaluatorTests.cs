using OpenGitBase.Api.Services;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Api.Tests.Services;

public class ReplicationStateEvaluatorTests
{
    private static readonly Guid NodeA = Guid.NewGuid();
    private static readonly Guid NodeB = Guid.NewGuid();
    private static readonly Guid NodeC = Guid.NewGuid();

    [Fact]
    public void RecoveredStateOrNull_Rf3_WhenAllReplicasInSyncOnHealthyNodes_ReturnsRf3Healthy()
    {
        var repo = Rf3Repo(primaryWatermark: 6, watermarkA: 6, watermarkB: 6, watermarkC: 6);
        var healthy = new HashSet<Guid> { NodeA, NodeB, NodeC };

        Assert.Equal(
            ReplicationState.Rf3Healthy,
            ReplicationStateEvaluator.RecoveredStateOrNull(repo, healthy)
        );
    }

    [Fact]
    public void RecoveredStateOrNull_Rf3_WhenFewerThanTwoInSync_ReturnsNull()
    {
        // Only the primary is caught up; both replicas still lag -> not recovered.
        var repo = Rf3Repo(primaryWatermark: 6, watermarkA: 6, watermarkB: 3, watermarkC: 2);
        var healthy = new HashSet<Guid> { NodeA, NodeB, NodeC };

        Assert.Null(ReplicationStateEvaluator.RecoveredStateOrNull(repo, healthy));
    }

    [Fact]
    public void RecoveredStateOrNull_Rf3_WhenInSyncReplicasSitOnUnhealthyNodes_ReturnsNull()
    {
        // All three are watermark-in-sync, but only NodeA is healthy -> quorum not met.
        var repo = Rf3Repo(primaryWatermark: 6, watermarkA: 6, watermarkB: 6, watermarkC: 6);
        var healthy = new HashSet<Guid> { NodeA };

        Assert.Null(ReplicationStateEvaluator.RecoveredStateOrNull(repo, healthy));
    }

    [Fact]
    public void RecoveredStateOrNull_Rf4_WhenEncryptedAndPlaintextInSync_ReturnsRf4Healthy()
    {
        var repo = new RepositoryEntity
        {
            Id = Guid.NewGuid(),
            PrimaryWatermark = 10,
            ReplicationState = ReplicationState.Degraded,
            Replicas =
            [
                Replica(NodeA, RepositoryReplicaRole.Primary, applied: 10),
                Replica(NodeB, RepositoryReplicaRole.Replica, applied: 10),
                Replica(NodeC, RepositoryReplicaRole.EncryptedReplica, applied: 0, artifact: 10),
            ],
        };
        var healthy = new HashSet<Guid> { NodeA, NodeB, NodeC };

        Assert.Equal(
            ReplicationState.Rf4Healthy,
            ReplicationStateEvaluator.RecoveredStateOrNull(repo, healthy)
        );
    }

    [Fact]
    public void RecoveredStateOrNull_Rf4_WhenEncryptedArtifactLags_ReturnsNull()
    {
        var repo = new RepositoryEntity
        {
            Id = Guid.NewGuid(),
            PrimaryWatermark = 10,
            ReplicationState = ReplicationState.Degraded,
            Replicas =
            [
                Replica(NodeA, RepositoryReplicaRole.Primary, applied: 10),
                Replica(NodeB, RepositoryReplicaRole.Replica, applied: 10),
                Replica(NodeC, RepositoryReplicaRole.EncryptedReplica, applied: 0, artifact: 4),
            ],
        };
        var healthy = new HashSet<Guid> { NodeA, NodeB, NodeC };

        Assert.Null(ReplicationStateEvaluator.RecoveredStateOrNull(repo, healthy));
    }

    private static RepositoryEntity Rf3Repo(
        long primaryWatermark,
        long watermarkA,
        long watermarkB,
        long watermarkC
    ) =>
        new()
        {
            Id = Guid.NewGuid(),
            PrimaryWatermark = primaryWatermark,
            ReplicationState = ReplicationState.Degraded,
            Replicas =
            [
                Replica(NodeA, RepositoryReplicaRole.Primary, watermarkA),
                Replica(NodeB, RepositoryReplicaRole.Replica, watermarkB),
                Replica(NodeC, RepositoryReplicaRole.Replica, watermarkC),
            ],
        };

    private static RepositoryReplicaEntity Replica(
        Guid nodeId,
        RepositoryReplicaRole role,
        long applied,
        long? artifact = null
    ) =>
        new()
        {
            StorageNodeId = nodeId,
            Role = role,
            AppliedWatermark = applied,
            ArtifactWatermark = artifact,
        };
}
