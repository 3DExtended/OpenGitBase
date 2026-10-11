using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Services;

public sealed class AntiEntropyReconcilerService
{
    private const int MtlsGitHttpPort = 8443;
    private const int MaxPendingPrimaryWritesPerPass = 10;

    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IQueryProcessor _queryProcessor;
    private readonly IStorageProvisionerClient _storageProvisionerClient;
    private readonly ReplicationArtifactPublisher _artifactPublisher;
    private readonly Rf1BackfillService _backfillService;
    private readonly HaStorageBackgroundOptions _options;
    private readonly ILogger<AntiEntropyReconcilerService> _logger;

    public AntiEntropyReconcilerService(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IQueryProcessor queryProcessor,
        IStorageProvisionerClient storageProvisionerClient,
        ReplicationArtifactPublisher artifactPublisher,
        Rf1BackfillService backfillService,
        IOptions<HaStorageBackgroundOptions> options,
        ILogger<AntiEntropyReconcilerService> logger
    )
    {
        _contextFactory = contextFactory;
        _queryProcessor = queryProcessor;
        _storageProvisionerClient = storageProvisionerClient;
        _artifactPublisher = artifactPublisher;
        _backfillService = backfillService;
        _options = options.Value;
        _logger = logger;
    }

    /// <summary>
    /// One reconcile pass. Each step runs on its own: every step but the last talks to storage
    /// nodes, and a node that does not answer (its client throws on transport failures) must cost
    /// only the step that asked it, never the rest of the pass. Pending primary writes go first:
    /// they are the only step that moves PrimaryWatermark, and the pushes behind them are what a
    /// user is waiting for (#413).
    /// </summary>
    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        await RunStepAsync(context, "pending primary writes", ReplicatePendingPrimaryWritesAsync, cancellationToken)
            .ConfigureAwait(false);
        await RunStepAsync(context, "encrypted artifact check", VerifyEncryptedArtifactPresenceAsync, cancellationToken)
            .ConfigureAwait(false);
        await RunStepAsync(context, "lagging replicas", SyncLaggingRepositoriesAsync, cancellationToken)
            .ConfigureAwait(false);
        await RunStepAsync(context, "degraded latch", HealRecoveredDegradedAsync, cancellationToken)
            .ConfigureAwait(false);

        var degraded = await context
            .Set<RepositoryEntity>()
            .CountAsync(
                repository =>
                    repository.ReplicationState == ReplicationState.Degraded
                    || repository.ReplicationState == ReplicationState.Rf1Backfilling,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (degraded > 0)
        {
            await _backfillService.RunOnceAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool IsFailure(Exception exception, CancellationToken cancellationToken) =>
        exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested;

    private static void Forget(OpenGitBaseDbContext context, RepositoryEntity repository)
    {
        foreach (var replica in repository.Replicas)
        {
            context.Entry(replica).State = EntityState.Detached;
        }

        context.Entry(repository).State = EntityState.Detached;
    }

    private async Task RunStepAsync(
        OpenGitBaseDbContext context,
        string step,
        Func<OpenGitBaseDbContext, CancellationToken, Task> run,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await run(context, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (IsFailure(ex, cancellationToken))
        {
            _logger.LogError(ex, "Reconcile step {Step} failed; the other steps still ran.", step);
            // Nothing a failed step changed and did not save may ride along with a later save.
            context.ChangeTracker.Clear();
        }
    }

    private async Task SyncLaggingRepositoriesAsync(
        OpenGitBaseDbContext context,
        CancellationToken cancellationToken
    )
    {
        var lagging = await context
            .Set<RepositoryEntity>()
            .Include(repository => repository.Replicas)
            .Where(repository =>
                repository.Replicas.Any(replica =>
                    replica.AppliedWatermark < repository.PrimaryWatermark
                    || (
                        replica.Role == RepositoryReplicaRole.EncryptedReplica
                        && (
                            replica.ArtifactWatermark == null
                            || replica.ArtifactWatermark < repository.PrimaryWatermark
                        )
                    )
                )
            )
            .Take(10)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var repository in lagging)
        {
            try
            {
                await SyncLaggingReplicasAsync(context, repository, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (IsFailure(ex, cancellationToken))
            {
                _logger.LogWarning(
                    ex,
                    "Could not bring the replicas of repository {RepositoryId} up to date; next pass.",
                    repository.Id
                );
                Forget(context, repository);
            }
        }
    }

    /// <summary>
    /// Replicates writes that reached the primary but never the quorum. The post-receive hook
    /// raises the primary's watermark file before it replicates, so after a failed replication
    /// (#413: every push of a repository whose bundle passed ~15 MB) the primary's heartbeat
    /// reports AppliedWatermark one above PrimaryWatermark, and nothing else ever looks at that.
    /// <para>
    /// Such a write is left to the push for <see cref="HaStorageBackgroundOptions.PrimaryAheadGraceSeconds"/>,
    /// measured from the primary replica's LastSyncedAt (moved by the heartbeat when it raises the
    /// watermark, and by quorum-replicate when a push starts replicating). After that the
    /// reconciler claims the repository by moving LastSyncedAt with a compare-and-set, so the
    /// other API instance's reconciler, which runs at the same times, skips it, and replicates
    /// the write through quorum-replicate's createArtifact path, which builds the artifact on the
    /// primary, stops uploading as soon as a push marks the primary again, and commits the
    /// watermark with a compare-and-set. One step per repository and pass; nothing is lowered.
    /// </para>
    /// </summary>
    private async Task ReplicatePendingPrimaryWritesAsync(
        OpenGitBaseDbContext context,
        CancellationToken cancellationToken
    )
    {
        var pending = await context
            .Set<RepositoryEntity>()
            .AsNoTracking()
            .Where(repository =>
                (
                    repository.ReplicationState == ReplicationState.Rf4Healthy
                    || repository.ReplicationState == ReplicationState.Rf3Healthy
                )
                && repository.PrimaryStorageNodeId != null
                && repository.Replicas.Any(replica =>
                    replica.StorageNodeId == repository.PrimaryStorageNodeId
                    && replica.Role == RepositoryReplicaRole.Primary
                    && replica.AppliedWatermark > repository.PrimaryWatermark
                )
            )
            .Select(repository => new
            {
                repository.Id,
                PrimaryNodeId = repository.PrimaryStorageNodeId!.Value,
                repository.PrimaryWatermark,
                PrimaryLastSyncedAt = repository
                    .Replicas.Where(replica => replica.StorageNodeId == repository.PrimaryStorageNodeId)
                    .Select(replica => replica.LastSyncedAt)
                    .FirstOrDefault(),
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (pending.Count == 0)
        {
            return;
        }

        // Filtered and ordered here rather than in SQL: SQLite cannot order DateTimeOffset values.
        // Longest-waiting first; a claim moves LastSyncedAt to now, so a write that keeps failing
        // goes to the back and cannot hold the pass's slots against the others.
        var cutoff = DateTimeOffset.UtcNow.AddSeconds(-_options.PrimaryAheadGraceSeconds);
        var due = pending
            .Where(write => write.PrimaryLastSyncedAt is null || write.PrimaryLastSyncedAt <= cutoff)
            .OrderBy(write => write.PrimaryLastSyncedAt ?? DateTimeOffset.MinValue)
            .Take(MaxPendingPrimaryWritesPerPass);
        foreach (var write in due)
        {
            try
            {
                await ReplicatePendingPrimaryWriteAsync(
                        context,
                        write.Id,
                        write.PrimaryNodeId,
                        write.PrimaryWatermark,
                        write.PrimaryLastSyncedAt,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (IsFailure(ex, cancellationToken))
            {
                // The claim stays: the write waits out another grace period, behind the others.
                _logger.LogWarning(
                    ex,
                    "Pending write {Watermark} of repository {RepositoryId} is still not replicated",
                    write.PrimaryWatermark + 1,
                    write.Id
                );
            }
        }
    }

    private async Task ReplicatePendingPrimaryWriteAsync(
        OpenGitBaseDbContext context,
        Guid repositoryId,
        Guid primaryNodeId,
        long primaryWatermark,
        DateTimeOffset? primaryLastSyncedAt,
        CancellationToken cancellationToken
    )
    {
        var claim = ReplicationClaim.Now();
        var claimed = await context
            .Set<RepositoryReplicaEntity>()
            .Where(replica =>
                replica.RepositoryId == repositoryId
                && replica.StorageNodeId == primaryNodeId
                && replica.LastSyncedAt == primaryLastSyncedAt
            )
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(replica => replica.LastSyncedAt, claim),
                cancellationToken
            )
            .ConfigureAwait(false);
        if (claimed == 0)
        {
            // The other API instance's reconciler, a push or a heartbeat got there first.
            return;
        }

        var watermark = primaryWatermark + 1;
        var result = await _queryProcessor
            .RunQueryAsync(
                new QuorumReplicateRepositoryQuery
                {
                    RepositoryId = RepositoryId.From(repositoryId),
                    StorageNodeId = StorageNodeId.From(primaryNodeId),
                    AppliedWatermark = watermark,
                    CreateArtifact = true,
                    ExpectedPrimaryLastSyncedAt = claim,
                },
                cancellationToken
            )
            .ConfigureAwait(false);
        if (result.IsSome && result.Get().Success)
        {
            _logger.LogInformation(
                "Replicated pending write {Watermark} of repository {RepositoryId} from its primary",
                watermark,
                repositoryId
            );
        }
        else
        {
            _logger.LogWarning(
                "Pending write {Watermark} of repository {RepositoryId} is still not replicated: {Error}",
                watermark,
                repositoryId,
                result.IsSome ? result.Get().Error : "no result"
            );
        }
    }

    /// <summary>
    /// Clears the <see cref="ReplicationState.Degraded"/> latch on repositories that have actually
    /// recovered. The membership-changing services (<c>Rf1BackfillService</c>,
    /// <c>RebalanceService</c>) only return a repo to a healthy state as a side effect of adding or
    /// replacing a replica; a repo that was already at full membership and merely caught back up
    /// otherwise stays Degraded forever. This pass re-evaluates each Degraded repo against the
    /// replication target on healthy nodes and promotes it to Rf3/Rf4 Healthy once it qualifies.
    /// </summary>
    private async Task HealRecoveredDegradedAsync(
        OpenGitBaseDbContext context,
        CancellationToken cancellationToken
    )
    {
        var degraded = await context
            .Set<RepositoryEntity>()
            .Include(repository => repository.Replicas)
            .Where(repository => repository.ReplicationState == ReplicationState.Degraded)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (degraded.Count == 0)
        {
            return;
        }

        var nodes = await _queryProcessor
            .RunQueryAsync(new ListHealthyStorageNodesQuery(), cancellationToken)
            .ConfigureAwait(false);
        if (nodes.IsNone)
        {
            return;
        }

        var healthyNodeIds = nodes.Get().Select(node => node.Id.Value).ToHashSet();
        var changed = false;
        foreach (var repository in degraded)
        {
            var recovered = ReplicationStateEvaluator.RecoveredStateOrNull(
                repository,
                healthyNodeIds
            );
            if (recovered is not null)
            {
                repository.ReplicationState = recovered.Value;
                changed = true;
            }
        }

        if (changed)
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Corrects false-healthy encrypted replicas. An EncryptedReplica whose DB
    /// <see cref="RepositoryReplicaEntity.ArtifactWatermark"/> claims it is caught up but whose
    /// on-disk artifacts are actually gone (e.g. the node was recreated without a persistent
    /// artifact volume) is otherwise invisible to the lagging query and never repaired. We probe
    /// each such node for its true on-disk artifact watermark and, when it is behind the stored
    /// value, lower the stored value so the normal repair pass re-uploads the artifact to the
    /// current <c>PrimaryWatermark</c> (no watermark inflation). Only ever corrects downward; an
    /// unavailable or failed probe is left untouched so a transient outage never marks a healthy
    /// replica as lagging.
    /// </summary>
    private async Task VerifyEncryptedArtifactPresenceAsync(
        OpenGitBaseDbContext context,
        CancellationToken cancellationToken
    )
    {
        var candidates = await context
            .Set<RepositoryEntity>()
            .Include(repository => repository.Replicas)
            .Where(repository =>
                repository.PrimaryWatermark > 0
                && repository.Replicas.Any(replica =>
                    replica.Role == RepositoryReplicaRole.EncryptedReplica
                    && replica.ArtifactWatermark != null
                )
            )
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (candidates.Count == 0)
        {
            return;
        }

        var nodes = await _queryProcessor
            .RunQueryAsync(new ListHealthyStorageNodesQuery(), cancellationToken)
            .ConfigureAwait(false);
        if (nodes.IsNone)
        {
            return;
        }

        var nodeById = nodes.Get().ToDictionary(node => node.Id.Value);
        var changed = false;

        foreach (var repository in candidates)
        {
            foreach (var replica in repository.Replicas)
            {
                if (
                    replica.Role != RepositoryReplicaRole.EncryptedReplica
                    || replica.ArtifactWatermark is null
                )
                {
                    continue;
                }

                if (!nodeById.TryGetValue(replica.StorageNodeId, out var node))
                {
                    continue;
                }

                var status = await ProbeArtifactWatermarkAsync(node, repository.Id, cancellationToken)
                    .ConfigureAwait(false);
                if (status is null || !status.Success)
                {
                    continue;
                }

                if ((status.ArtifactWatermark ?? -1) < replica.ArtifactWatermark.Value)
                {
                    replica.ArtifactWatermark = status.ArtifactWatermark;
                    changed = true;
                }
            }
        }

        if (changed)
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>The node's on-disk artifact watermark, or null when it could not be asked.</summary>
    private async Task<ArtifactWatermarkStatusResult?> ProbeArtifactWatermarkAsync(
        StorageNodeDto node,
        Guid repositoryId,
        CancellationToken cancellationToken
    )
    {
        try
        {
            var token = await GetApiTokenAsync(node.Id, cancellationToken).ConfigureAwait(false);
            if (token is null)
            {
                return null;
            }

            return await _storageProvisionerClient
                .TryGetArtifactWatermarkStatusAsync(node, token, repositoryId, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception ex) when (IsFailure(ex, cancellationToken))
        {
            _logger.LogWarning(
                ex,
                "Could not ask {NodeId} for the artifact watermark of repository {RepositoryId}",
                node.NodeId,
                repositoryId
            );
            return null;
        }
    }

    private async Task SyncLaggingReplicasAsync(
        OpenGitBaseDbContext context,
        RepositoryEntity repository,
        CancellationToken cancellationToken
    )
    {
        var primaryReplica = repository.Replicas.FirstOrDefault(replica =>
            replica.StorageNodeId == repository.PrimaryStorageNodeId
        );
        if (primaryReplica is null)
        {
            return;
        }

        var nodes = await _queryProcessor
            .RunQueryAsync(new ListHealthyStorageNodesQuery(), cancellationToken)
            .ConfigureAwait(false);
        if (nodes.IsNone)
        {
            return;
        }

        var nodeById = nodes.Get().ToDictionary(node => node.Id.Value);
        if (!nodeById.TryGetValue(primaryReplica.StorageNodeId, out var primaryNode))
        {
            return;
        }

        ReplicationArtifactFetchResult? bootstrappedArtifact = null;

        foreach (var replica in repository.Replicas)
        {
            if (replica.Role == RepositoryReplicaRole.EncryptedReplica)
            {
                if (
                    replica.ArtifactWatermark is null
                    || replica.ArtifactWatermark < repository.PrimaryWatermark
                )
                {
                    await RepairEncryptedArtifactAsync(
                            repository,
                            replica,
                            primaryNode,
                            nodeById,
                            () => bootstrappedArtifact,
                            artifact => bootstrappedArtifact = artifact,
                            cancellationToken
                        )
                        .ConfigureAwait(false);
                }

                continue;
            }

            if (!ReplicationSync.IsInSync(replica.AppliedWatermark, repository.PrimaryWatermark))
            {
                if (!nodeById.TryGetValue(replica.StorageNodeId, out var replicaNode))
                {
                    continue;
                }

                var token = await GetApiTokenAsync(replicaNode.Id, cancellationToken)
                    .ConfigureAwait(false);
                if (token is null)
                {
                    continue;
                }

                var sync = await _storageProvisionerClient
                    .SyncRepositoryFromPeerAsync(
                        replicaNode,
                        token,
                        repository.PhysicalPath,
                        primaryNode.InternalHost,
                        repository.PhysicalPath,
                        MtlsGitHttpPort,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                if (sync.Success)
                {
                    replica.AppliedWatermark = repository.PrimaryWatermark;
                    replica.LastSyncedAt = DateTimeOffset.UtcNow;
                }
            }
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task RepairEncryptedArtifactAsync(
        RepositoryEntity repository,
        RepositoryReplicaEntity replica,
        StorageNodeDto primaryNode,
        IReadOnlyDictionary<Guid, StorageNodeDto> nodeById,
        Func<ReplicationArtifactFetchResult?> getBootstrappedArtifact,
        Action<ReplicationArtifactFetchResult> setBootstrappedArtifact,
        CancellationToken cancellationToken
    )
    {
        if (!nodeById.TryGetValue(replica.StorageNodeId, out var targetNode))
        {
            return;
        }

        var targetToken = await GetApiTokenAsync(targetNode.Id, cancellationToken)
            .ConfigureAwait(false);
        if (targetToken is null)
        {
            return;
        }

        if (repository.PrimaryWatermark <= 0)
        {
            replica.ArtifactWatermark = repository.PrimaryWatermark;
            replica.LastSyncedAt = DateTimeOffset.UtcNow;
            return;
        }

        var artifact = await ResolveEncryptedArtifactAsync(
                repository,
                primaryNode,
                nodeById,
                getBootstrappedArtifact,
                setBootstrappedArtifact,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (artifact is null || !artifact.Success)
        {
            return;
        }

        var upload = await _artifactPublisher
            .UploadAsync(targetNode, repository.Id, repository.PrimaryWatermark, artifact, cancellationToken)
            .ConfigureAwait(false);
        if (upload.Success)
        {
            replica.ArtifactWatermark = repository.PrimaryWatermark;
            replica.LastSyncedAt = DateTimeOffset.UtcNow;
        }
    }

    private async Task<ReplicationArtifactFetchResult?> ResolveEncryptedArtifactAsync(
        RepositoryEntity repository,
        StorageNodeDto primaryNode,
        IReadOnlyDictionary<Guid, StorageNodeDto> nodeById,
        Func<ReplicationArtifactFetchResult?> getBootstrappedArtifact,
        Action<ReplicationArtifactFetchResult> setBootstrappedArtifact,
        CancellationToken cancellationToken
    )
    {
        var cached = getBootstrappedArtifact();
        if (cached is not null)
        {
            return cached;
        }

        var sourceReplica = repository.Replicas
            .Where(candidate =>
                candidate.Role == RepositoryReplicaRole.EncryptedReplica
                && candidate.ArtifactWatermark >= repository.PrimaryWatermark
            )
            .FirstOrDefault();
        if (sourceReplica is not null
            && nodeById.TryGetValue(sourceReplica.StorageNodeId, out var sourceNode))
        {
            var sourceToken = await GetApiTokenAsync(sourceNode.Id, cancellationToken)
                .ConfigureAwait(false);
            if (sourceToken is not null)
            {
                var fromPeer = await _storageProvisionerClient
                    .TryGetReplicationArtifactAsync(
                        sourceNode,
                        sourceToken,
                        repository.Id,
                        repository.PrimaryWatermark,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                if (fromPeer.Success)
                {
                    setBootstrappedArtifact(fromPeer);
                    return fromPeer;
                }
            }
        }

        var created = await _artifactPublisher
            .CreateOnPrimaryAsync(
                primaryNode,
                repository.Id,
                repository.PhysicalPath,
                repository.PrimaryWatermark,
                repository.ReplicationEpoch,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (created.Success)
        {
            setBootstrappedArtifact(created);
        }

        return created;
    }

    private async Task<string?> GetApiTokenAsync(
        StorageNodeId storageNodeId,
        CancellationToken cancellationToken
    )
    {
        var result = await _queryProcessor
            .RunQueryAsync(
                new GetStorageNodeApiTokenQuery { StorageNodeId = storageNodeId },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsSome ? result.Get() : null;
    }
}
