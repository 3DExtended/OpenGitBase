using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Common.Storage;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Services;

public sealed class ColdRecoveryService : IColdRecoveryService
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IQueryProcessor _queryProcessor;
    private readonly IRepositoryKeyService _repositoryKeyService;
    private readonly IEncryptedArtifactService _encryptedArtifactService;
    private readonly IStorageProvisionerClient _storageProvisionerClient;
    private readonly ILfsRepairQueue _lfsRepairs;
    private readonly ILogger<ColdRecoveryService> _logger;

    public ColdRecoveryService(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IQueryProcessor queryProcessor,
        IRepositoryKeyService repositoryKeyService,
        IEncryptedArtifactService encryptedArtifactService,
        IStorageProvisionerClient storageProvisionerClient,
        ILfsRepairQueue lfsRepairs,
        ILogger<ColdRecoveryService> logger
    )
    {
        _contextFactory = contextFactory;
        _queryProcessor = queryProcessor;
        _repositoryKeyService = repositoryKeyService;
        _encryptedArtifactService = encryptedArtifactService;
        _storageProvisionerClient = storageProvisionerClient;
        _lfsRepairs = lfsRepairs;
        _logger = logger;
    }

    public async Task<bool> TryRecoverAsync(
        Guid repositoryId,
        CancellationToken cancellationToken = default
    )
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var entity = await context
            .Set<RepositoryEntity>()
            .Include(repository => repository.Replicas)
            .FirstOrDefaultAsync(repository => repository.Id == repositoryId, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return false;
        }

        entity.ReplicationState = ReplicationState.Recovering;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var key = await _repositoryKeyService
            .TryGetRepositoryKeyAsync(repositoryId, cancellationToken)
            .ConfigureAwait(false);

        if (key is null)
        {
            _logger.LogWarning("Cold recovery aborted: repository key unavailable for {RepositoryId}", repositoryId);
            return false;
        }

        var encryptedReplica = entity.Replicas
            .Where(replica => replica.Role == RepositoryReplicaRole.EncryptedReplica)
            .OrderByDescending(replica => replica.ArtifactWatermark ?? -1)
            .FirstOrDefault();

        if (encryptedReplica?.ArtifactWatermark is null or <= 0)
        {
            _logger.LogWarning("Cold recovery aborted: no encrypted artifact watermark for {RepositoryId}", repositoryId);
            return false;
        }

        var encryptedNode = await LoadStorageNodeAsync(
                StorageNodeId.From(encryptedReplica.StorageNodeId),
                cancellationToken
            )
            .ConfigureAwait(false);
        var encryptedToken = encryptedNode is null
            ? null
            : await GetApiTokenAsync(encryptedNode.Id, cancellationToken).ConfigureAwait(false);
        if (encryptedNode is null || encryptedToken is null)
        {
            return false;
        }

        var artifact = await _storageProvisionerClient
            .TryGetReplicationArtifactAsync(
                encryptedNode,
                encryptedToken,
                repositoryId,
                encryptedReplica.ArtifactWatermark.Value,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (!artifact.Success)
        {
            return false;
        }

        byte[] bundlePlaintext;
        try
        {
            using var manifestDocument = JsonDocument.Parse(artifact.ManifestJson);
            var epoch = manifestDocument.RootElement.GetProperty("epoch").GetInt64();
            var watermark = manifestDocument.RootElement.GetProperty("watermark").GetInt64();
            bundlePlaintext = _encryptedArtifactService.DecryptBundle(
                artifact.BundlePayload,
                key.KeyMaterial,
                repositoryId,
                watermark,
                epoch
            );
            var manifest = EncryptedArtifactService.DeserializeManifest(artifact.ManifestJson);
            _encryptedArtifactService.VerifyEnvelope(
                new EncryptedArtifactEnvelope(manifest, artifact.BundlePayload),
                key.KeyMaterial,
                repositoryId
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Cold recovery integrity verification failed for {RepositoryId}", repositoryId);
            return false;
        }

        var healthyNodes = await _queryProcessor
            .RunQueryAsync(new ListHealthyStorageNodesQuery(), cancellationToken)
            .ConfigureAwait(false);
        var nodes = healthyNodes.IsSome ? healthyNodes.Get() : Array.Empty<StorageNodeDto>();
        var targetNode = SelectTarget(nodes, entity);

        if (targetNode is null)
        {
            return false;
        }

        var targetToken = await GetApiTokenAsync(targetNode.Id, cancellationToken).ConfigureAwait(false);
        if (targetToken is null)
        {
            return false;
        }

        var importResult = await _storageProvisionerClient
            .ImportRepositoryBundleAsync(
                targetNode,
                targetToken,
                entity.PhysicalPath,
                bundlePlaintext,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (!importResult.Success)
        {
            return false;
        }

        entity.ReplicationEpoch += 1;
        entity.PrimaryStorageNodeId = targetNode.Id.Value;
        entity.StorageNodeId = targetNode.Id.Value;
        entity.PrimaryWatermark = encryptedReplica.ArtifactWatermark.Value;
        entity.ReplicationState = ReplicationState.Rf4Healthy;

        if (entity.Replicas.All(replica => replica.StorageNodeId != targetNode.Id.Value))
        {
            // Routing finds the primary through its replica row; a node new to the repository needs one.
            entity.Replicas.Add(
                new RepositoryReplicaEntity { RepositoryId = repositoryId, StorageNodeId = targetNode.Id.Value }
            );
        }

        foreach (var replica in entity.Replicas)
        {
            if (replica.StorageNodeId == targetNode.Id.Value)
            {
                replica.Role = RepositoryReplicaRole.Primary;
                replica.AppliedWatermark = entity.PrimaryWatermark;
            }
            else if (replica.Role == RepositoryReplicaRole.Primary)
            {
                replica.Role = RepositoryReplicaRole.Replica;
            }
        }

        await ForgetLostPlaintextCopiesAsync(context, repositoryId, targetNode.Id.Value, cancellationToken)
            .ConfigureAwait(false);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Cold recovery completed for repository {RepositoryId}", repositoryId);

        // Git is back and the repository is writable. Its LFS objects are decrypted from the encrypted replicas in
        // the background; downloads of objects not restored yet answer 503 with Retry-After meanwhile.
        _lfsRepairs.Schedule(repositoryId);
        return true;
    }

    /// <summary>
    /// A node that holds no encrypted copy of the repository comes first, so the recovery keeps both encrypted
    /// copies; placement follows free space, so an encrypted replica can sit on any node. Among those, nodes outside
    /// the platform's encrypted layout come first. Only when every healthy node holds an encrypted copy does one of
    /// them become the primary (with one encrypted copy left until backfill adds another).
    /// </summary>
    private static StorageNodeDto? SelectTarget(IReadOnlyList<StorageNodeDto> nodes, RepositoryEntity entity)
    {
        var encryptedReplicas = entity
            .Replicas.Where(replica => replica.Role == RepositoryReplicaRole.EncryptedReplica)
            .Select(replica => replica.StorageNodeId)
            .ToHashSet();
        var withoutEncryptedCopy = nodes.Where(node => !encryptedReplicas.Contains(node.Id.Value)).ToList();
        return withoutEncryptedCopy.FirstOrDefault(node => !IsPlatformEncryptedNode(node))
            ?? withoutEncryptedCopy.FirstOrDefault()
            ?? nodes.FirstOrDefault(node => !IsPlatformEncryptedNode(node))
            ?? nodes.FirstOrDefault();
    }

    private static bool IsPlatformEncryptedNode(StorageNodeDto node) =>
        string.Equals(node.NodeId, PlatformRf4FleetLayout.EncryptedReplicaNodeIdA, StringComparison.Ordinal)
        || string.Equals(node.NodeId, PlatformRf4FleetLayout.EncryptedReplicaNodeIdB, StringComparison.Ordinal);

    /// <summary>
    /// The plaintext copies on the other nodes went with the git data, so their rows go too: the copy rows then
    /// show the restore's progress. A node that comes back with its copies gets them recorded again by the next
    /// LFS reconcile. Rows on the target are kept, so a repeated recovery keeps the copies restored so far.
    /// </summary>
    private static async Task ForgetLostPlaintextCopiesAsync(
        OpenGitBaseDbContext context,
        Guid repositoryId,
        Guid targetNodeId,
        CancellationToken cancellationToken
    )
    {
        var lost = await context
            .Set<LfsObjectCopyEntity>()
            .Where(copy =>
                copy.RepositoryId == repositoryId
                && copy.Kind == LfsObjectCopyKind.Plaintext
                && copy.StorageNodeId != targetNodeId
            )
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        context.RemoveRange(lost);
    }

    private async Task<StorageNodeDto?> LoadStorageNodeAsync(
        StorageNodeId storageNodeId,
        CancellationToken cancellationToken
    )
    {
        var result = await _queryProcessor
            .RunQueryAsync(
                new GetStorageNodeQuery { ModelId = storageNodeId },
                cancellationToken
            )
            .ConfigureAwait(false);

        return result.IsSome ? result.Get() : null;
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
