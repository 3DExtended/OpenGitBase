using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Services;

/// <summary>
/// Copies a verified LFS object from the write primary to the repository's other storage nodes. Encrypted replicas
/// get ciphertext the API produces while streaming (<see cref="LfsObjectCipher"/>), so the Repository Key never
/// leaves the API; plaintext replicas get the object as is.
/// <para>
/// <see cref="ReachQuorumAsync"/> returns as soon as one encrypted replica confirmed its copy.
/// <see cref="CompleteCopiesAsync"/> finishes the rest afterwards, in a DI scope of its own
/// (<see cref="ILfsCopyCompletionScheduler"/>). Whatever stays missing, the reconciler repairs.
/// </para>
/// </summary>
public sealed class LfsReplicationService
{
    private const string EncryptedReplicaRole = "EncryptedReplica";
    private readonly IQueryProcessor _queryProcessor;
    private readonly IStorageLfsClient _storage;
    private readonly IRepositoryKeyService _keys;
    private readonly LfsOptions _options;
    private readonly ILogger<LfsReplicationService> _logger;

    public LfsReplicationService(
        IQueryProcessor queryProcessor,
        IStorageLfsClient storage,
        IRepositoryKeyService keys,
        LfsOptions options,
        ILogger<LfsReplicationService> logger
    )
    {
        _queryProcessor = queryProcessor;
        _storage = storage;
        _keys = keys;
        _options = options;
        _logger = logger;
    }

    /// <summary>The healthy nodes of the repository, or null when its write primary is unknown or unhealthy.</summary>
    public async Task<LfsReplicaSet?> ResolveReplicaSetAsync(Guid repositoryId, CancellationToken cancellationToken)
    {
        var routing = await _queryProcessor
            .RunQueryAsync(
                new RepositoryReplicationRoutingQuery { RepositoryId = RepositoryId.From(repositoryId) },
                cancellationToken
            )
            .ConfigureAwait(false);
        if (routing.IsNone || routing.Get().Targets.FirstOrDefault(target => target.IsPrimary) is not { IsHealthy: true } primaryTarget)
        {
            return null;
        }

        var primary = await ResolveEndpointAsync(
                primaryTarget.StorageNodeId,
                primaryTarget.InternalHost,
                primaryTarget.InternalHttpPort,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (primary is null)
        {
            return null;
        }

        var plaintextReplicas = new List<StorageNodeEndpoint>();
        foreach (var target in routing.Get().Targets)
        {
            if (target.IsPrimary || !target.IsHealthy || target.StorageNodeId == primary.StorageNodeId)
            {
                continue;
            }

            if (await ResolveEndpointAsync(target.StorageNodeId, target.InternalHost, target.InternalHttpPort, cancellationToken)
                    .ConfigureAwait(false) is { } endpoint)
            {
                plaintextReplicas.Add(endpoint);
            }
        }

        var encryptedReplicas = new List<StorageNodeEndpoint>();
        var context = await _queryProcessor
            .RunQueryAsync(
                new GetRepositoryReplicationContextQuery
                {
                    RepositoryId = RepositoryId.From(repositoryId),
                    StorageNodeId = StorageNodeId.From(primary.StorageNodeId),
                },
                cancellationToken
            )
            .ConfigureAwait(false);
        var peers = context.IsSome ? context.Get().Peers : [];
        foreach (var peer in peers.Where(peer => peer.IsHealthy && peer.Role == EncryptedReplicaRole))
        {
            if (await ResolveEndpointAsync(peer.StorageNodeId, peer.InternalHost, peer.InternalHttpPort, cancellationToken)
                    .ConfigureAwait(false) is { } endpoint)
            {
                encryptedReplicas.Add(endpoint);
            }
        }

        return new LfsReplicaSet(repositoryId, primary, plaintextReplicas, encryptedReplicas);
    }

    /// <summary>
    /// Streams the object from the primary, encrypts it and uploads it to every encrypted replica in parallel, each
    /// upload capped by <see cref="LfsOptions.ReplicaUploadTimeoutSeconds"/>. Returns at the first confirmed copy.
    /// </summary>
    public async Task<LfsQuorumResult> ReachQuorumAsync(
        LfsReplicaSet replicas,
        string oid,
        long size,
        CancellationToken cancellationToken
    )
    {
        var key = await _keys.TryGetRepositoryKeyAsync(replicas.RepositoryId, cancellationToken).ConfigureAwait(false);
        if (key is null)
        {
            _logger.LogWarning("LFS object {Oid}: repository {RepositoryId} has no Repository Key", oid, replicas.RepositoryId);
            return new LfsQuorumResult(LfsQuorumStatus.KeyUnavailable, 0, [], null);
        }

        var cipher = new LfsCipherContext(key.KeyMaterial, key.KeyVersion, replicas.RepositoryId, oid);
        var uploads = replicas
            .EncryptedReplicas.Select(node => Task.Run(() => CopyEncryptedAsync(replicas.Primary, node, cipher, size)))
            .ToList();
        var running = uploads.ToList();
        while (running.Count > 0)
        {
            var finished = await Task.WhenAny(running).WaitAsync(cancellationToken).ConfigureAwait(false);
            running.Remove(finished);
            var attempt = await finished.ConfigureAwait(false);
            if (!attempt.Succeeded)
            {
                continue;
            }

            LfsObjectCopySpec[] copies =
            [
                new() { StorageNodeId = replicas.Primary.StorageNodeId, Kind = LfsObjectCopyKind.Plaintext },
                new() { StorageNodeId = attempt.Node.StorageNodeId, Kind = LfsObjectCopyKind.Encrypted },
            ];
            var pending = new LfsPendingCopies(
                replicas,
                oid,
                size,
                cipher,
                uploads,
                copies.Select(copy => copy.StorageNodeId).ToHashSet()
            );
            return new LfsQuorumResult(LfsQuorumStatus.Reached, key.KeyVersion, copies, pending);
        }

        return new LfsQuorumResult(LfsQuorumStatus.NotReached, key.KeyVersion, [], null);
    }

    /// <summary>
    /// Waits for the encrypted uploads verify did not wait for, retries each failed one once, copies the object to
    /// the plaintext replicas, and records every copy that now exists.
    /// </summary>
    public async Task CompleteCopiesAsync(LfsPendingCopies pending, CancellationToken cancellationToken)
    {
        var replicas = pending.Replicas;
        foreach (var attempt in await Task.WhenAll(pending.EncryptedUploads).ConfigureAwait(false))
        {
            if (pending.RecordedNodeIds.Contains(attempt.Node.StorageNodeId))
            {
                continue;
            }

            var stored = attempt.Succeeded
                || (await CopyEncryptedAsync(replicas.Primary, attempt.Node, pending.Cipher, pending.Size).ConfigureAwait(false)).Succeeded;
            if (stored)
            {
                await RecordCopyAsync(pending, attempt.Node, LfsObjectCopyKind.Encrypted, cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        foreach (var node in replicas.PlaintextReplicas)
        {
            if ((await CopyPlaintextAsync(replicas.Primary, node, pending.Oid, pending.Size, replicas.RepositoryId).ConfigureAwait(false)).Succeeded)
            {
                await RecordCopyAsync(pending, node, LfsObjectCopyKind.Plaintext, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Streams the object from a plaintext <paramref name="source"/>, encrypts it and stores it on
    /// <paramref name="target"/>, capped by <see cref="LfsOptions.ReplicaUploadTimeoutSeconds"/>. Never throws: a
    /// failure is an unsuccessful attempt.
    /// </summary>
    public async Task<LfsCopyAttempt> CopyEncryptedAsync(
        StorageNodeEndpoint source,
        StorageNodeEndpoint target,
        LfsCipherContext cipher,
        long size,
        CancellationToken cancellationToken = default
    )
    {
        using var timeout = TransferTimeout(cancellationToken);
        try
        {
            var plaintext = await _storage
                .OpenObjectAsync(source, cipher.RepositoryId, cipher.Oid, LfsObjectCopyKind.Plaintext, timeout.Token)
                .ConfigureAwait(false);
            if (plaintext is null)
            {
                _logger.LogWarning("LFS object {Oid} is missing on {Source}", cipher.Oid, source.InternalHost);
                return new LfsCopyAttempt(target, LfsObjectCopyKind.Encrypted, false);
            }

            using var content = new LfsEncryptingContent(plaintext, size, cipher, _options.SegmentBytes);
            await _storage
                .PutObjectAsync(target, cipher.RepositoryId, cipher.Oid, LfsObjectCopyKind.Encrypted, content, timeout.Token)
                .ConfigureAwait(false);
            return new LfsCopyAttempt(target, LfsObjectCopyKind.Encrypted, true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Encrypted copy of LFS object {Oid} on {Node} failed", cipher.Oid, target.InternalHost);
            return new LfsCopyAttempt(target, LfsObjectCopyKind.Encrypted, false);
        }
    }

    /// <summary>
    /// Streams the object from a plaintext <paramref name="source"/> to <paramref name="target"/> unless the target
    /// already holds it. The storage node checks the bytes against the oid. Never throws.
    /// </summary>
    public async Task<LfsCopyAttempt> CopyPlaintextAsync(
        StorageNodeEndpoint source,
        StorageNodeEndpoint target,
        string oid,
        long size,
        Guid repositoryId,
        CancellationToken cancellationToken = default
    )
    {
        using var timeout = TransferTimeout(cancellationToken);
        try
        {
            var held = await _storage
                .GetObjectSizeAsync(target, repositoryId, oid, LfsObjectCopyKind.Plaintext, timeout.Token)
                .ConfigureAwait(false);
            if (held == size)
            {
                return new LfsCopyAttempt(target, LfsObjectCopyKind.Plaintext, true);
            }

            var plaintext = await _storage
                .OpenObjectAsync(source, repositoryId, oid, LfsObjectCopyKind.Plaintext, timeout.Token)
                .ConfigureAwait(false);
            if (plaintext is null)
            {
                return new LfsCopyAttempt(target, LfsObjectCopyKind.Plaintext, false);
            }

            using var content = new StreamContent(plaintext);
            content.Headers.ContentLength = size;
            await _storage
                .PutObjectAsync(target, repositoryId, oid, LfsObjectCopyKind.Plaintext, content, timeout.Token)
                .ConfigureAwait(false);
            return new LfsCopyAttempt(target, LfsObjectCopyKind.Plaintext, true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Plaintext copy of LFS object {Oid} on {Node} failed", oid, target.InternalHost);
            return new LfsCopyAttempt(target, LfsObjectCopyKind.Plaintext, false);
        }
    }

    /// <summary>
    /// Restores the plaintext object on <paramref name="target"/> from an encrypted copy on
    /// <paramref name="encryptedSource"/>, decrypting while streaming (<see cref="LfsDecryptingContent"/>). A copy
    /// that fails authentication or the oid check is never written. Never throws.
    /// </summary>
    public async Task<LfsCopyAttempt> RestorePlaintextAsync(
        StorageNodeEndpoint encryptedSource,
        StorageNodeEndpoint target,
        LfsCipherContext cipher,
        long size,
        CancellationToken cancellationToken = default
    )
    {
        using var timeout = TransferTimeout(cancellationToken);
        try
        {
            var ciphertext = await _storage
                .OpenObjectAsync(encryptedSource, cipher.RepositoryId, cipher.Oid, LfsObjectCopyKind.Encrypted, timeout.Token)
                .ConfigureAwait(false);
            if (ciphertext is null)
            {
                _logger.LogWarning("Encrypted copy of LFS object {Oid} is missing on {Source}", cipher.Oid, encryptedSource.InternalHost);
                return new LfsCopyAttempt(target, LfsObjectCopyKind.Plaintext, false);
            }

            using var content = new LfsDecryptingContent(ciphertext, size, cipher);
            await _storage
                .PutObjectAsync(target, cipher.RepositoryId, cipher.Oid, LfsObjectCopyKind.Plaintext, content, timeout.Token)
                .ConfigureAwait(false);
            return new LfsCopyAttempt(target, LfsObjectCopyKind.Plaintext, true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Restoring LFS object {Oid} on {Node} from the encrypted copy on {Source} failed",
                cipher.Oid,
                target.InternalHost,
                encryptedSource.InternalHost
            );
            return new LfsCopyAttempt(target, LfsObjectCopyKind.Plaintext, false);
        }
    }

    private async Task<StorageNodeEndpoint?> ResolveEndpointAsync(
        Guid storageNodeId,
        string host,
        int httpPort,
        CancellationToken cancellationToken
    )
    {
        var token = await _queryProcessor
            .RunQueryAsync(
                new GetStorageNodeApiTokenQuery { StorageNodeId = StorageNodeId.From(storageNodeId) },
                cancellationToken
            )
            .ConfigureAwait(false);
        return token.IsNone || string.IsNullOrEmpty(host)
            ? null
            : new StorageNodeEndpoint(storageNodeId, host, httpPort, token.Get());
    }

    /// <summary>One transfer's cap, cancelled early with the caller's token.</summary>
    private CancellationTokenSource TransferTimeout(CancellationToken cancellationToken)
    {
        var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(_options.ReplicaUploadTimeoutSeconds));
        return timeout;
    }

    private Task RecordCopyAsync(
        LfsPendingCopies pending,
        StorageNodeEndpoint node,
        LfsObjectCopyKind kind,
        CancellationToken cancellationToken
    ) =>
        _queryProcessor.RunQueryAsync(
            new RecordLfsObjectCopyQuery
            {
                RepositoryId = RepositoryId.From(pending.Replicas.RepositoryId),
                Oid = pending.Oid,
                StorageNodeId = node.StorageNodeId,
                Kind = kind,
            },
            cancellationToken
        );
}
