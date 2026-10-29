using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Services;

public sealed class Rf4BackfillService
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IQueryProcessor _queryProcessor;
    private readonly IRepositoryKeyService _repositoryKeyService;
    private readonly IStorageProvisionerClient _storageProvisionerClient;
    private readonly RepositoryStorageQuotaOptions _quotaOptions;

    public Rf4BackfillService(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IQueryProcessor queryProcessor,
        IRepositoryKeyService repositoryKeyService,
        IStorageProvisionerClient storageProvisionerClient,
        IOptions<RepositoryStorageQuotaOptions> quotaOptions
    )
    {
        _contextFactory = contextFactory;
        _queryProcessor = queryProcessor;
        _repositoryKeyService = repositoryKeyService;
        _storageProvisionerClient = storageProvisionerClient;
        _quotaOptions = quotaOptions.Value;
    }

    public async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var candidates = await context
            .Set<RepositoryEntity>()
            .Include(repository => repository.Replicas)
            .Where(repository =>
                repository.ReplicationState == ReplicationState.Rf3Healthy
                || repository.ReplicationState == ReplicationState.Rf4Migrating
            )
            .Take(5)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var repository in candidates)
        {
            await MigrateRepositoryAsync(context, repository, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// The planner's layout around the repository's current primary, or null while that primary is not healthy
    /// (failover moves it first). The planner always prefers its own primary (storage-1 on the platform layout) and
    /// may name the current primary as an encrypted replica. Taking its roles as they stood left "Notes" (Oct 2026)
    /// with storage-1's row Primary and storage-3's EncryptedReplica while PrimaryStorageNodeId stayed on storage-3:
    /// every clone and push was denied. The current primary holds the up-to-date plaintext, so it stays the primary,
    /// with the read replica colocated as the platform layout has it. The encrypted replicas are the planner's three
    /// nodes without the current primary, encrypted slots first: where the planner put the current primary into an
    /// encrypted slot, its own primary choice (healthy, with room, not the other encrypted replica) takes that slot.
    /// </summary>
    private static IReadOnlyList<(StorageNodeDto Node, RepositoryReplicaRole Role)>? TargetsAroundCurrentPrimary(
        RepositoryEntity repository,
        ReplicaSetSelection selection,
        IReadOnlyList<StorageNodeDto> healthyNodes
    )
    {
        var primary = healthyNodes.FirstOrDefault(node => node.Id.Value == repository.PrimaryStorageNodeId);
        if (primary is null)
        {
            return null;
        }

        var encrypted = new[] { selection.EncryptedReplicaA, selection.EncryptedReplicaB, selection.Primary }
            .Where(node => node.Id != primary.Id)
            .Take(2)
            .Select(node => (node, RepositoryReplicaRole.EncryptedReplica));
        return [(primary, RepositoryReplicaRole.Primary), .. encrypted];
    }

    private async Task MigrateRepositoryAsync(
        OpenGitBaseDbContext context,
        RepositoryEntity repository,
        CancellationToken cancellationToken
    )
    {
        if (repository.ReplicationState == ReplicationState.Rf3Healthy)
        {
            // The roles are planned around the current primary, so it has to be unambiguous. An inconsistent one is
            // left to an operator: planning from it could make a node without the repository the primary.
            if (RepositoryPrimaryConsistency.FindProblems(repository).Count > 0)
            {
                return;
            }

            repository.ReplicationState = ReplicationState.Rf4Migrating;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            await _repositoryKeyService
                .GenerateAndStoreKeyAsync(repository.Id, cancellationToken)
                .ConfigureAwait(false);

            var healthyNodes = await _queryProcessor
                .RunQueryAsync(new ListHealthyStorageNodesQuery(), cancellationToken)
                .ConfigureAwait(false);
            var nodes = healthyNodes.IsSome ? healthyNodes.Get() : Array.Empty<StorageNodeDto>();
            var selection = ReplicaSetPlanner.SelectReplicaSet(nodes);
            var targets = selection is null ? null : TargetsAroundCurrentPrimary(repository, selection, nodes);
            if (targets is null)
            {
                repository.ReplicationState = ReplicationState.Rf3Healthy;
                await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return;
            }

            var existingNodeIds = repository.Replicas.Select(replica => replica.StorageNodeId).ToHashSet();
            foreach (var target in targets)
            {
                if (existingNodeIds.Contains(target.Node.Id.Value))
                {
                    var existing = repository.Replicas.First(replica =>
                        replica.StorageNodeId == target.Node.Id.Value
                    );
                    existing.Role = target.Role;
                    continue;
                }

                var token = await GetApiTokenAsync(target.Node.Id, cancellationToken).ConfigureAwait(false);
                if (token is null)
                {
                    continue;
                }

                var receiveMaxBytes = _quotaOptions.Enabled ? _quotaOptions.MaxFileBytes : 0L;
                var provision = await _storageProvisionerClient
                    .ProvisionRepositoryAsync(
                        target.Node,
                        token,
                        repository.PhysicalPath,
                        receiveMaxBytes,
                        target.Role.ToString(),
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                if (!provision.Success)
                {
                    continue;
                }

                repository.Replicas.Add(
                    new RepositoryReplicaEntity
                    {
                        RepositoryId = repository.Id,
                        StorageNodeId = target.Node.Id.Value,
                        Role = target.Role,
                        AppliedWatermark = repository.PrimaryWatermark,
                    }
                );
            }

            repository.ReadReplicaStorageNodeId = repository.PrimaryStorageNodeId;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        var seeded = await SeedEncryptedArtifactsAsync(repository, cancellationToken)
            .ConfigureAwait(false);
        if (!seeded)
        {
            return;
        }

        repository.ReplicationState = ReplicationState.Rf4Healthy;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task<bool> SeedEncryptedArtifactsAsync(
        RepositoryEntity repository,
        CancellationToken cancellationToken
    )
    {
        var encryptedReplicas = repository.Replicas
            .Where(replica => replica.Role == RepositoryReplicaRole.EncryptedReplica)
            .ToList();
        if (encryptedReplicas.Count < 2)
        {
            return false;
        }

        if (encryptedReplicas.All(replica =>
                replica.ArtifactWatermark >= repository.PrimaryWatermark
            ))
        {
            return true;
        }

        var nodes = await _queryProcessor
            .RunQueryAsync(new ListHealthyStorageNodesQuery(), cancellationToken)
            .ConfigureAwait(false);
        if (nodes.IsNone)
        {
            return false;
        }

        var nodeById = nodes.Get().ToDictionary(node => node.Id.Value);
        if (
            repository.PrimaryStorageNodeId is null
            || !nodeById.TryGetValue(repository.PrimaryStorageNodeId.Value, out var primaryNode)
        )
        {
            return false;
        }

        if (repository.PrimaryWatermark <= 0)
        {
            var now = DateTimeOffset.UtcNow;
            foreach (var replica in encryptedReplicas)
            {
                replica.ArtifactWatermark = repository.PrimaryWatermark;
                replica.LastSyncedAt = now;
            }

            return true;
        }

        var primaryToken = await GetApiTokenAsync(primaryNode.Id, cancellationToken)
            .ConfigureAwait(false);
        if (primaryToken is null)
        {
            return false;
        }

        var key = await _repositoryKeyService
            .TryGetRepositoryKeyAsync(repository.Id, cancellationToken)
            .ConfigureAwait(false);
        if (key is null)
        {
            return false;
        }

        var artifact = await _storageProvisionerClient
            .CreateReplicationArtifactAsync(
                primaryNode,
                primaryToken,
                repository.PhysicalPath,
                repository.Id,
                repository.PrimaryWatermark,
                repository.ReplicationEpoch,
                Convert.ToHexString(key.KeyMaterial).ToLowerInvariant(),
                key.KeyVersion,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (!artifact.Success)
        {
            return false;
        }

        var nowUtc = DateTimeOffset.UtcNow;
        foreach (var replica in encryptedReplicas)
        {
            if (replica.ArtifactWatermark >= repository.PrimaryWatermark)
            {
                continue;
            }

            if (!nodeById.TryGetValue(replica.StorageNodeId, out var targetNode))
            {
                return false;
            }

            var targetToken = await GetApiTokenAsync(targetNode.Id, cancellationToken)
                .ConfigureAwait(false);
            if (targetToken is null)
            {
                return false;
            }

            var upload = await _storageProvisionerClient
                .UploadReplicationArtifactAsync(
                    targetNode,
                    targetToken,
                    repository.Id,
                    repository.PrimaryWatermark,
                    artifact.ManifestJson,
                    artifact.BundlePayload,
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (!upload.Success)
            {
                return false;
            }

            replica.ArtifactWatermark = repository.PrimaryWatermark;
            replica.LastSyncedAt = nowUtc;
        }

        return true;
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
