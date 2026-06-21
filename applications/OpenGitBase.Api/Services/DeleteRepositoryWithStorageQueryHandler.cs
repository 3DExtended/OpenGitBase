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

        var targetNodeIds = entity.Replicas.Count > 0
            ? entity.Replicas.Select(replica => replica.StorageNodeId).ToList()
            : [entity.StorageNodeId.Value];

        var deleteResults = new List<(Guid NodeId, bool Success)>();
        foreach (var nodeId in targetNodeIds)
        {
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

            var deleteResult = await _storageProvisionerClient
                .DeleteRepositoryAsync(
                    storageNode.Get(),
                    apiToken.Get(),
                    entity.PhysicalPath,
                    cancellationToken
                )
                .ConfigureAwait(false);
            deleteResults.Add((nodeId, deleteResult.Success));
        }

        var successCount = deleteResults.Count(result => result.Success);
        var requiredQuorum = entity.Replicas.Count >= 3 ? 2 : 1;
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
