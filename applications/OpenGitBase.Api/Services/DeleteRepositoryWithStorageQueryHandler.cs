using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Services;

public sealed class DeleteRepositoryWithStorageQueryHandler
    : IQueryHandler<DeleteRepositoryWithStorageQuery, DeleteRepositoryWithStorageResult>
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly IStorageProvisionerClient _storageProvisionerClient;
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IStorageLfsClient _lfsClient;
    private readonly ILogger<DeleteRepositoryWithStorageQueryHandler> _logger;

    public DeleteRepositoryWithStorageQueryHandler(
        IQueryProcessor queryProcessor,
        IStorageProvisionerClient storageProvisionerClient,
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IStorageLfsClient lfsClient,
        ILogger<DeleteRepositoryWithStorageQueryHandler> logger
    )
    {
        _queryProcessor = queryProcessor;
        _storageProvisionerClient = storageProvisionerClient;
        _contextFactory = contextFactory;
        _lfsClient = lfsClient;
        _logger = logger;
    }

    public async Task<Option<DeleteRepositoryWithStorageResult>> RunQueryAsync(
        DeleteRepositoryWithStorageQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        var entity = await context
            .Set<RepositoryEntity>()
            .Include(repository => repository.Replicas)
            .FirstOrDefaultAsync(repository => repository.Id == query.Id.Value, cancellationToken)
            .ConfigureAwait(false);

        if (entity is null)
        {
            return Option<DeleteRepositoryWithStorageResult>.None;
        }

        if (!entity.StorageNodeId.HasValue)
        {
            return Option.From(
                DeleteRepositoryWithStorageResult.Failed(
                    "Repository is not assigned to a storage node."
                )
            );
        }

        var deleteResults = new List<(Guid NodeId, bool Success)>();
        foreach (var replica in entity.Replicas.DefaultIfEmpty(
                     new RepositoryReplicaEntity
                     {
                         StorageNodeId = entity.StorageNodeId!.Value,
                         Role = RepositoryReplicaRole.Primary,
                     }
                 ))
        {
            var nodeId = replica.StorageNodeId;
            var storageNodeId = StorageNodeId.From(nodeId);
            var storageNode = await _queryProcessor
                .RunQueryAsync(
                    new GetStorageNodeQuery { ModelId = storageNodeId },
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (storageNode.IsNone || !storageNode.Get().IsHealthy)
            {
                deleteResults.Add((nodeId, false));
                continue;
            }

            var apiToken = await _queryProcessor
                .RunQueryAsync(
                    new GetStorageNodeApiTokenQuery { StorageNodeId = storageNodeId },
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (apiToken.IsNone)
            {
                deleteResults.Add((nodeId, false));
                continue;
            }

            var deleteSuccess = true;
            if (replica.Role == RepositoryReplicaRole.EncryptedReplica)
            {
                for (var watermark = 1L; watermark <= entity.PrimaryWatermark; watermark++)
                {
                    var artifactDelete = await _storageProvisionerClient
                        .DeleteReplicationArtifactAsync(
                            storageNode.Get(),
                            apiToken.Get(),
                            entity.Id,
                            watermark,
                            cancellationToken
                        )
                        .ConfigureAwait(false);
                    deleteSuccess &= artifactDelete.Success || artifactDelete.StatusCode == 404;
                }
            }
            else
            {
                var deleteResult = await _storageProvisionerClient
                    .DeleteRepositoryAsync(
                        storageNode.Get(),
                        apiToken.Get(),
                        entity.PhysicalPath,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
                deleteSuccess = deleteResult.Success;
            }

            deleteResults.Add((nodeId, deleteSuccess));
        }

        var successCount = deleteResults.Count(result => result.Success);
        var requiredQuorum = entity.ReplicationState == ReplicationState.Rf4Healthy ? 3 : entity.Replicas.Count >= 3 ? 2 : 1;
        if (successCount < requiredQuorum)
        {
            return Option.From(
                DeleteRepositoryWithStorageResult.Failed(
                    "Quorum delete failed: fewer than two storage nodes confirmed deletion."
                )
            );
        }

        var pendingScrubNodeIds = deleteResults
            .Where(result => !result.Success)
            .Select(result => result.NodeId)
            .ToList();
        var physicalPath = entity.PhysicalPath;
        var lfsNodeIds = await FindLfsNodeIdsAsync(context, entity, cancellationToken).ConfigureAwait(false);
        context.Remove(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // The LfsObject and LfsObjectCopy rows went with the repository (cascade). The data goes only now, so a
        // failed quorum above never loses LFS objects of a repository that still exists.
        await DeleteLfsDataAsync(entity.Id, lfsNodeIds, cancellationToken).ConfigureAwait(false);

        if (pendingScrubNodeIds.Count > 0)
        {
            _ = Task.Run(
                () => ScrubPendingNodesAsync(physicalPath, pendingScrubNodeIds, CancellationToken.None),
                CancellationToken.None
            );
        }

        return Option.From(DeleteRepositoryWithStorageResult.Deleted());
    }

    /// <summary>Every node that may hold LFS data of the repository: its replicas, its primary, and any node a copy
    /// row names (a node that left the replica set keeps its copies until they are deleted).</summary>
    private static async Task<List<Guid>> FindLfsNodeIdsAsync(
        OpenGitBaseDbContext context,
        RepositoryEntity entity,
        CancellationToken cancellationToken
    )
    {
        var copyNodeIds = await context
            .Set<LfsObjectCopyEntity>()
            .AsNoTracking()
            .Where(copy => copy.RepositoryId == entity.Id)
            .Select(copy => copy.StorageNodeId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        IEnumerable<Guid?> primaries = [entity.StorageNodeId, entity.PrimaryStorageNodeId];
        return entity.Replicas
            .Select(replica => replica.StorageNodeId)
            .Concat(primaries.OfType<Guid>())
            .Concat(copyNodeIds)
            .Distinct()
            .ToList();
    }

    /// <summary>
    /// Asks each node to delete the repository's LFS data. A node that is down or fails keeps it until the
    /// reconciler finds the directory of a repository that no longer exists and deletes it then.
    /// </summary>
    private async Task DeleteLfsDataAsync(
        Guid repositoryId,
        IReadOnlyList<Guid> nodeIds,
        CancellationToken cancellationToken
    )
    {
        foreach (var nodeId in nodeIds)
        {
            var storageNodeId = StorageNodeId.From(nodeId);
            var storageNode = await _queryProcessor
                .RunQueryAsync(new GetStorageNodeQuery { ModelId = storageNodeId }, cancellationToken)
                .ConfigureAwait(false);
            var apiToken = await _queryProcessor
                .RunQueryAsync(new GetStorageNodeApiTokenQuery { StorageNodeId = storageNodeId }, cancellationToken)
                .ConfigureAwait(false);
            if (storageNode.IsNone || !storageNode.Get().IsHealthy || apiToken.IsNone)
            {
                _logger.LogWarning(
                    "LFS data of deleted repository {RepositoryId} stays on unavailable node {StorageNodeId} until the reconciler removes it.",
                    repositoryId,
                    nodeId
                );
                continue;
            }

            var node = storageNode.Get();
            try
            {
                await _lfsClient
                    .DeleteRepositoryAsync(
                        new StorageNodeEndpoint(nodeId, node.InternalHost, node.InternalHttpPort, apiToken.Get()),
                        repositoryId,
                        cancellationToken
                    )
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    ex,
                    "Deleting LFS data of repository {RepositoryId} on node {StorageNodeId} failed; the reconciler removes it later.",
                    repositoryId,
                    nodeId
                );
            }
        }
    }

    private async Task ScrubPendingNodesAsync(
        string physicalPath,
        IReadOnlyList<Guid> nodeIds,
        CancellationToken cancellationToken
    )
    {
        foreach (var nodeId in nodeIds)
        {
            var storageNodeId = StorageNodeId.From(nodeId);
            var storageNode = await _queryProcessor
                .RunQueryAsync(
                    new GetStorageNodeQuery { ModelId = storageNodeId },
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (storageNode.IsNone)
            {
                continue;
            }

            var apiToken = await _queryProcessor
                .RunQueryAsync(
                    new GetStorageNodeApiTokenQuery { StorageNodeId = storageNodeId },
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (apiToken.IsNone)
            {
                continue;
            }

            await _storageProvisionerClient
                .DeleteRepositoryAsync(
                    storageNode.Get(),
                    apiToken.Get(),
                    physicalPath,
                    cancellationToken
                )
                .ConfigureAwait(false);
        }
    }
}
