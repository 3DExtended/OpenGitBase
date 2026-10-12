using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Api.Services;

/// <summary>
/// Decides whether a repository currently marked <see cref="ReplicationState.Degraded"/> has
/// actually recovered — i.e. its replicas again meet the replication target on healthy nodes —
/// and, if so, which healthy state it should return to.
/// </summary>
/// <remarks>
/// <para>
/// The <c>Degraded</c> flag is set when a replica lags, a node goes unhealthy, or a backfill
/// cannot reach the replication factor. Historically nothing cleared it once the fleet recovered
/// unless replica <em>membership</em> changed (a backfill added a replica, or a rebalance replaced
/// one off a dead node). A repo that was already at full membership and simply caught back up
/// therefore stayed <c>Degraded</c> forever. This evaluator is the periodic re-check that closes
/// that gap.
/// </para>
/// </remarks>
public static class ReplicationStateEvaluator
{
    /// <summary>
    /// Copies an RF4 repository needs in sync to count as healthy, at least one of them
    /// plaintext (normally the primary) and one encrypted.
    /// </summary>
    public const int Rf4RequiredCopiesInSync = 3;

    /// <summary>
    /// Whether an RF4 repository meets its replication target. <see cref="ReplicaSetPlanner"/>
    /// lays RF4 out as the primary plus two encrypted replicas, so there is only one plaintext
    /// copy; older repos may still carry an extra plaintext replica. Requiring two plaintext
    /// copies (as this used to) can never hold for the planner's layout and left every RF4
    /// repository that went Degraded there for good, refusing all writes.
    /// </summary>
    public static bool MeetsRf4Target(int plaintextInSync, int encryptedInSync) =>
        plaintextInSync >= 1
        && encryptedInSync >= 1
        && plaintextInSync + encryptedInSync >= Rf4RequiredCopiesInSync;

    /// <summary>
    /// Returns the healthy state a <see cref="ReplicationState.Degraded"/> repository should be
    /// promoted to now, or <c>null</c> if it does not yet qualify (leave it Degraded). Only ever
    /// promotes upward out of Degraded; it never demotes and only considers replicas on healthy
    /// nodes so a repo is not declared healthy while its copies sit on down nodes.
    /// </summary>
    public static ReplicationState? RecoveredStateOrNull(
        RepositoryEntity repository,
        IReadOnlySet<Guid> healthyNodeIds
    )
    {
        var plaintextInSync = repository.Replicas.Count(replica =>
            replica.Role != RepositoryReplicaRole.EncryptedReplica
            && healthyNodeIds.Contains(replica.StorageNodeId)
            && ReplicationSync.IsInSync(replica.AppliedWatermark, repository.PrimaryWatermark)
        );

        var hasEncryptedReplica = repository.Replicas.Any(replica =>
            replica.Role == RepositoryReplicaRole.EncryptedReplica
        );

        if (hasEncryptedReplica)
        {
            var encryptedInSync = repository.Replicas.Count(replica =>
                replica.Role == RepositoryReplicaRole.EncryptedReplica
                && healthyNodeIds.Contains(replica.StorageNodeId)
                && replica.ArtifactWatermark >= repository.PrimaryWatermark
            );
            return MeetsRf4Target(plaintextInSync, encryptedInSync)
                ? ReplicationState.Rf4Healthy
                : null;
        }

        return plaintextInSync >= 2 ? ReplicationState.Rf3Healthy : null;
    }
}
