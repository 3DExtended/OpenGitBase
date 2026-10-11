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

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);

        await VerifyEncryptedArtifactPresenceAsync(context, cancellationToken).ConfigureAwait(false);

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
            await SyncLaggingReplicasAsync(context, repository, cancellationToken)
                .ConfigureAwait(false);
        }

        await ReplicatePendingPrimaryWritesAsync(context, cancellationToken).ConfigureAwait(false);

        await HealRecoveredDegradedAsync(context, cancellationToken).ConfigureAwait(false);

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

        // Filtered here rather than in SQL: SQLite cannot order DateTimeOffset values.
        var cutoff = DateTimeOffset.UtcNow.AddSeconds(-_options.PrimaryAheadGraceSeconds);
        var due = pending
            .Where(write => write.PrimaryLastSyncedAt is null || write.PrimaryLastSyncedAt <= cutoff)
            .Take(MaxPendingPrimaryWritesPerPass);
        foreach (var write in due)
        {
            var claim = ReplicationClaim.Now();
            var claimed = await context
                .Set<RepositoryReplicaEntity>()
                .Where(replica =>
                    replica.RepositoryId == write.Id
                    && replica.StorageNodeId == write.PrimaryNodeId
                    && replica.LastSyncedAt == write.PrimaryLastSyncedAt
                )
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(replica => replica.LastSyncedAt, claim),
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (claimed == 0)
            {
                // The other API instance's reconciler, a push or a heartbeat got there first.
                continue;
            }

            var watermark = write.PrimaryWatermark + 1;
            var result = await _queryProcessor
                .RunQueryAsync(
                    new QuorumReplicateRepositoryQuery
                    {
                        RepositoryId = RepositoryId.From(write.Id),
                        StorageNodeId = StorageNodeId.From(write.PrimaryNodeId),
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
                    write.Id
                );
            }
            else
            {
                _logger.LogWarning(
                    "Pending write {Watermark} of repository {RepositoryId} is still not replicated: {Error}",
                    watermark,
                    write.Id,
                    result.IsSome ? result.Get().Error : "no result"
                );
            }
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

                var token = await GetApiTokenAsync(node.Id, cancellationToken).ConfigureAwait(false);
                if (token is null)
                {
                    continue;
                }

                var status = await _storageProvisionerClient
                    .TryGetArtifactWatermarkStatusAsync(
                        node,
                        token,
                        repository.Id,
                        cancellationToken
                    )
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
