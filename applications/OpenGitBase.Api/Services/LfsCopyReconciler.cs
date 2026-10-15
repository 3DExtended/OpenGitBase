using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Services;

/// <summary>
/// The reconciler's LFS step for one repository (lfs-05). It decides from what each node holds on disk, not from
/// the <see cref="LfsObjectCopyEntity"/> rows, which is the lesson of the false-healthy artifact loss (#230):
/// <list type="number">
/// <item>It reads the LFS inventory of every healthy node of the repository. A node that does not answer is skipped,
/// and its rows stay as they are.</item>
/// <item>It compares the inventories with the recorded objects. A copy found gets <c>LastVerifiedAt</c>, and a row
/// if it had none. A copy not found loses its row.</item>
/// <item>It repairs missing copies, plaintext first, so that a plaintext copy restored from an encrypted replica can
/// feed the encrypted repairs of the same run. Within each kind the longest-missing copy goes first, and a run makes
/// at most <see cref="LfsOptions.MaxRepairsPerRun"/> repairs.</item>
/// </list>
/// A replica newly assigned by rebalance or backfill has an empty inventory, so the same path fills it.
/// </summary>
public sealed class LfsCopyReconciler : ILfsCopyReconciler
{
    private const string NoKey = "the repository has no Repository Key";
    private static readonly List<NodeState> NoSources = [];
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IQueryProcessor _queryProcessor;
    private readonly IStorageLfsClient _storage;
    private readonly LfsReplicationService _transfers;
    private readonly IRepositoryKeyService _keys;
    private readonly LfsOptions _options;
    private readonly ILogger<LfsCopyReconciler> _logger;

    public LfsCopyReconciler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IQueryProcessor queryProcessor,
        IStorageLfsClient storage,
        LfsReplicationService transfers,
        IRepositoryKeyService keys,
        LfsOptions options,
        ILogger<LfsCopyReconciler> logger
    )
    {
        _contextFactory = contextFactory;
        _queryProcessor = queryProcessor;
        _storage = storage;
        _transfers = transfers;
        _keys = keys;
        _options = options;
        _logger = logger;
    }

    public async Task<LfsReconcileResult> ReconcileRepositoryAsync(Guid repositoryId, CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);
        var objects = await context
            .Set<LfsObjectEntity>()
            .AsNoTracking()
            .Where(entity => entity.RepositoryId == repositoryId)
            .Select(entity => new RecordedObject(entity.Oid, entity.Size, entity.CreatedAt))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (objects.Count == 0)
        {
            return LfsReconcileResult.Nothing;
        }

        var nodes = await ReadInventoriesAsync(context, repositoryId, cancellationToken).ConfigureAwait(false);
        var copies = await context
            .Set<LfsObjectCopyEntity>()
            .Where(entity => entity.RepositoryId == repositoryId)
            .ToDictionaryAsync(entity => (entity.Oid, entity.StorageNodeId), cancellationToken)
            .ConfigureAwait(false);
        var missing = Compare(context, repositoryId, objects, nodes, copies);
        await SaveAsync(context, repositoryId, cancellationToken).ConfigureAwait(false);

        // Ordered here rather than in SQL: SQLite cannot order DateTimeOffset values.
        var due = missing
            .OrderBy(copy => copy.Node.Kind == LfsObjectCopyKind.Plaintext ? 0 : 1)
            .ThenBy(copy => copy.MissingSince)
            .Take(Math.Max(1, _options.MaxRepairsPerRun))
            .ToList();
        var repairs = new RepairRun(repositoryId, nodes);
        var repaired = 0;
        foreach (var copy in due)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (await RepairAsync(context, repairs, copy, cancellationToken).ConfigureAwait(false))
            {
                repaired++;
            }
        }

        return new LfsReconcileResult(missing.Count, repaired, due.Count - repaired, missing.Count - due.Count);
    }

    private static bool IsFailure(Exception exception, CancellationToken cancellationToken) =>
        exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested;

    private static bool Holds(LfsObjectCopyKind kind, RecordedObject recorded, long storedSize) =>
        kind == LfsObjectCopyKind.Plaintext
            ? storedSize == recorded.Size
            : LfsObjectCipher.IsPlausibleEncryptedLength(recorded.Size, storedSize);

    private static LfsObjectCopyEntity Row(Guid repositoryId, string oid, NodeState node, DateTimeOffset now) =>
        new()
        {
            RepositoryId = repositoryId,
            Oid = oid,
            StorageNodeId = node.Endpoint.StorageNodeId,
            Kind = node.Kind,
            StoredAt = now,
            LastVerifiedAt = now,
        };

    private async Task<IReadOnlyList<NodeState>> ReadInventoriesAsync(
        OpenGitBaseDbContext context,
        Guid repositoryId,
        CancellationToken cancellationToken
    )
    {
        var replicas = await context
            .Set<RepositoryReplicaEntity>()
            .AsNoTracking()
            .Where(replica => replica.RepositoryId == repositoryId)
            .Select(replica => new { replica.StorageNodeId, replica.Role })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var healthy = await _queryProcessor
            .RunQueryAsync(new ListHealthyStorageNodesQuery(), cancellationToken)
            .ConfigureAwait(false);
        var healthyById = healthy.IsSome ? healthy.Get().ToDictionary(node => node.Id.Value) : [];

        var nodes = new List<NodeState>();
        foreach (var replica in replicas.GroupBy(replica => replica.StorageNodeId))
        {
            // An unhealthy node is not asked; its rows stay as they are until it is back.
            if (!healthyById.TryGetValue(replica.Key, out var node))
            {
                continue;
            }

            var kind = replica.Any(role => role.Role == RepositoryReplicaRole.EncryptedReplica)
                ? LfsObjectCopyKind.Encrypted
                : LfsObjectCopyKind.Plaintext;
            var token = await _queryProcessor
                .RunQueryAsync(new GetStorageNodeApiTokenQuery { StorageNodeId = node.Id }, cancellationToken)
                .ConfigureAwait(false);
            if (token.IsNone)
            {
                continue;
            }

            var endpoint = new StorageNodeEndpoint(node.Id.Value, node.InternalHost, node.InternalHttpPort, token.Get());
            try
            {
                var inventory = await _storage.GetInventoryAsync(endpoint, repositoryId, cancellationToken).ConfigureAwait(false);
                if (inventory.Kind != kind)
                {
                    _logger.LogWarning(
                        "Storage node {Node} reports {Actual} LFS copies of {RepositoryId}, but its role holds {Expected}; skipped",
                        endpoint.InternalHost,
                        inventory.Kind,
                        repositoryId,
                        kind
                    );
                    continue;
                }

                nodes.Add(new NodeState(endpoint, kind, inventory.Objects));
            }
            catch (Exception ex) when (IsFailure(ex, cancellationToken))
            {
                _logger.LogWarning(
                    ex,
                    "Could not read the LFS inventory of {Node} for {RepositoryId}; its LFS copies stay as recorded",
                    endpoint.InternalHost,
                    repositoryId
                );
            }
        }

        return nodes;
    }

    private List<MissingCopy> Compare(
        OpenGitBaseDbContext context,
        Guid repositoryId,
        IReadOnlyList<RecordedObject> objects,
        IReadOnlyList<NodeState> nodes,
        Dictionary<(string Oid, Guid StorageNodeId), LfsObjectCopyEntity> copies
    )
    {
        var now = DateTimeOffset.UtcNow;
        var missing = new List<MissingCopy>();
        foreach (var node in nodes)
        {
            foreach (var recorded in objects)
            {
                copies.TryGetValue((recorded.Oid, node.Endpoint.StorageNodeId), out var row);
                if (node.Inventory.TryGetValue(recorded.Oid, out var storedSize) && Holds(node.Kind, recorded, storedSize))
                {
                    node.Holding.Add(recorded.Oid);
                    if (row is null)
                    {
                        context.Add(Row(repositoryId, recorded.Oid, node, now));
                    }
                    else
                    {
                        row.Kind = node.Kind;
                        row.LastVerifiedAt = now;
                    }

                    continue;
                }

                missing.Add(new MissingCopy(recorded, node, row?.LastVerifiedAt ?? row?.StoredAt ?? recorded.CreatedAt));
                if (row is null)
                {
                    // Never stored there: a new replica, or a copy the verify completion could not make.
                    _logger.LogInformation(
                        "LFS object {Oid} of {RepositoryId} missing on {Node} (never recorded there)",
                        recorded.Oid,
                        repositoryId,
                        node.Endpoint.InternalHost
                    );
                    continue;
                }

                context.Remove(row);
                _logger.LogWarning(
                    "LFS object {Oid} of {RepositoryId} missing on {Node} although recorded there; its copy row is removed",
                    recorded.Oid,
                    repositoryId,
                    node.Endpoint.InternalHost
                );
            }
        }

        return missing;
    }

    private async Task<bool> RepairAsync(
        OpenGitBaseDbContext context,
        RepairRun run,
        MissingCopy copy,
        CancellationToken cancellationToken
    )
    {
        var oid = copy.Object.Oid;
        var target = copy.Node;
        var sources = run.Nodes.Where(node => node != target && node.Holding.Contains(oid)).ToList();
        var plaintextSources = sources.Where(node => node.Kind == LfsObjectCopyKind.Plaintext).ToList();
        var encryptedSources = sources.Where(node => node.Kind == LfsObjectCopyKind.Encrypted).ToList();
        string? repairedFrom = null;
        var reason = "no reachable node holds a good copy";

        if (target.Kind == LfsObjectCopyKind.Plaintext)
        {
            foreach (var source in plaintextSources)
            {
                var attempt = await _transfers
                    .CopyPlaintextAsync(source.Endpoint, target.Endpoint, oid, copy.Object.Size, run.RepositoryId, cancellationToken)
                    .ConfigureAwait(false);
                if (attempt.Succeeded)
                {
                    repairedFrom = source.Endpoint.InternalHost;
                    break;
                }

                reason = "every copy from another plaintext node failed";
            }

            if (repairedFrom is null && encryptedSources.Count > 0)
            {
                var cipher = await run.CipherAsync(_keys, oid, cancellationToken).ConfigureAwait(false);
                reason = cipher is null ? NoKey : "every encrypted copy failed to decrypt or upload";
                foreach (var source in cipher is null ? NoSources : encryptedSources)
                {
                    var attempt = await _transfers
                        .RestorePlaintextAsync(source.Endpoint, target.Endpoint, cipher!, copy.Object.Size, cancellationToken)
                        .ConfigureAwait(false);
                    if (attempt.Succeeded)
                    {
                        repairedFrom = $"{source.Endpoint.InternalHost} (decrypted)";
                        break;
                    }
                }
            }
        }
        else if (plaintextSources.Count > 0)
        {
            var cipher = await run.CipherAsync(_keys, oid, cancellationToken).ConfigureAwait(false);
            reason = cipher is null ? NoKey : "every encryption from a plaintext copy failed";
            foreach (var source in cipher is null ? NoSources : plaintextSources)
            {
                var attempt = await _transfers
                    .CopyEncryptedAsync(source.Endpoint, target.Endpoint, cipher!, copy.Object.Size, cancellationToken)
                    .ConfigureAwait(false);
                if (attempt.Succeeded)
                {
                    repairedFrom = $"{source.Endpoint.InternalHost} (encrypted)";
                    break;
                }
            }
        }

        if (repairedFrom is null)
        {
            _logger.LogWarning(
                "Could not repair LFS object {Oid} of {RepositoryId} on {Node}: {Reason}",
                oid,
                run.RepositoryId,
                target.Endpoint.InternalHost,
                reason
            );
            return false;
        }

        target.Holding.Add(oid);
        context.Add(Row(run.RepositoryId, oid, target, DateTimeOffset.UtcNow));
        await SaveAsync(context, run.RepositoryId, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation(
            "Repaired LFS object {Oid} of {RepositoryId} on {Node} from {Source}",
            oid,
            run.RepositoryId,
            target.Endpoint.InternalHost,
            repairedFrom
        );
        return true;
    }

    /// <summary>Saves the bookkeeping. A row another writer changed first only costs this save, never the repairs.</summary>
    private async Task SaveAsync(OpenGitBaseDbContext context, Guid repositoryId, CancellationToken cancellationToken)
    {
        try
        {
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogInformation(ex, "LFS copy rows of {RepositoryId} changed meanwhile; the next run records them", repositoryId);
            context.ChangeTracker.Clear();
        }
    }

    private sealed record RecordedObject(string Oid, long Size, DateTimeOffset CreatedAt);

    private sealed record MissingCopy(RecordedObject Object, NodeState Node, DateTimeOffset MissingSince);

    /// <summary>A node whose inventory was read: the oids it holds a good copy of, extended by this run's repairs.</summary>
    private sealed class NodeState(StorageNodeEndpoint endpoint, LfsObjectCopyKind kind, IReadOnlyDictionary<string, long> inventory)
    {
        public StorageNodeEndpoint Endpoint { get; } = endpoint;

        public LfsObjectCopyKind Kind { get; } = kind;

        public IReadOnlyDictionary<string, long> Inventory { get; } = inventory;

        public HashSet<string> Holding { get; } = new(StringComparer.Ordinal);
    }

    /// <summary>One run's nodes and its Repository Key, fetched once and only when a repair needs it.</summary>
    private sealed class RepairRun(Guid repositoryId, IReadOnlyList<NodeState> nodes)
    {
        private bool _keyFetched;
        private EphemeralRepositoryKey? _key;

        public Guid RepositoryId { get; } = repositoryId;

        public IReadOnlyList<NodeState> Nodes { get; } = nodes;

        public async Task<LfsCipherContext?> CipherAsync(IRepositoryKeyService keys, string oid, CancellationToken cancellationToken)
        {
            if (!_keyFetched)
            {
                _key = await keys.TryGetRepositoryKeyAsync(RepositoryId, cancellationToken).ConfigureAwait(false);
                _keyFetched = true;
            }

            return _key is null ? null : new LfsCipherContext(_key.KeyMaterial, _key.KeyVersion, RepositoryId, oid);
        }
    }
}
