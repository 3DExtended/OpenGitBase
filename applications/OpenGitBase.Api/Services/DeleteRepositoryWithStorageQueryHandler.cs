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

    public DeleteRepositoryWithStorageQueryHandler(
        IQueryProcessor queryProcessor,
        IStorageProvisionerClient storageProvisionerClient,
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _queryProcessor = queryProcessor;
        _storageProvisionerClient = storageProvisionerClient;
        _contextFactory = contextFactory;
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
        context.Remove(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        if (pendingScrubNodeIds.Count > 0)
        {
            _ = Task.Run(
                () => ScrubPendingNodesAsync(physicalPath, pendingScrubNodeIds, CancellationToken.None),
                CancellationToken.None
            );
        }

        return Option.From(DeleteRepositoryWithStorageResult.Deleted());
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
