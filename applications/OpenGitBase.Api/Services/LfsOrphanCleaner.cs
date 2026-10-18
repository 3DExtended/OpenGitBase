using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Services;

public sealed class LfsOrphanCleaner : ILfsOrphanCleaner
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly IStorageProvisionerClient _storageProvisionerClient;
    private readonly IStorageLfsClient _lfsClient;
    private readonly ILogger<LfsOrphanCleaner> _logger;

    public LfsOrphanCleaner(
        IQueryProcessor queryProcessor,
        IStorageProvisionerClient storageProvisionerClient,
        IStorageLfsClient lfsClient,
        ILogger<LfsOrphanCleaner> logger
    )
    {
        _queryProcessor = queryProcessor;
        _storageProvisionerClient = storageProvisionerClient;
        _lfsClient = lfsClient;
        _logger = logger;
    }

    /// <summary>
    /// Asks every healthy node which repositories it holds LFS data for, then deletes the data of each one without
    /// a repository row. The node is asked first and the database second, so a repository created in between is
    /// seen as existing. A control plane that sees no repository at all (a fresh or wrong database) deletes nothing.
    /// A node that cannot be asked or fails a delete costs only its own cleanup.
    /// </summary>
    public async Task RunOnceAsync(OpenGitBaseDbContext context, CancellationToken cancellationToken)
    {
        var nodes = await _queryProcessor
            .RunQueryAsync(new ListHealthyStorageNodesQuery(), cancellationToken)
            .ConfigureAwait(false);
        foreach (var node in nodes.IsSome ? nodes.Get() : [])
        {
            var token = await _queryProcessor
                .RunQueryAsync(new GetStorageNodeApiTokenQuery { StorageNodeId = node.Id }, cancellationToken)
                .ConfigureAwait(false);
            if (token.IsNone)
            {
                continue;
            }

            StorageInventoryResult inventory;
            try
            {
                inventory = await _storageProvisionerClient
                    .TryGetInventoryAsync(node, token.Get(), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Storage node {NodeId} did not report its LFS data; its orphans wait for the next pass.", node.NodeId);
                continue;
            }

            if (!inventory.Success || inventory.LfsRepositories.Count == 0)
            {
                continue;
            }

            var candidates = inventory.LfsRepositories.ToList();
            if (!await context.Set<RepositoryEntity>().AnyAsync(cancellationToken).ConfigureAwait(false))
            {
                _logger.LogWarning(
                    "Storage node {NodeId} holds LFS data of {Count} repositories, but the database has no repository at all; nothing is deleted.",
                    node.NodeId,
                    candidates.Count
                );
                continue;
            }

            var existing = await context
                .Set<RepositoryEntity>()
                .AsNoTracking()
                .Where(repository => candidates.Contains(repository.Id))
                .Select(repository => repository.Id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            var endpoint = new StorageNodeEndpoint(node.Id.Value, node.InternalHost, node.InternalHttpPort, token.Get());
            foreach (var orphan in candidates.Except(existing))
            {
                await DeleteAsync(endpoint, node.NodeId, orphan, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task DeleteAsync(
        StorageNodeEndpoint endpoint,
        string nodeId,
        Guid repositoryId,
        CancellationToken cancellationToken
    )
    {
        try
        {
            await _lfsClient.DeleteRepositoryAsync(endpoint, repositoryId, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "Deleted LFS data of repository {RepositoryId}, which no longer exists, on storage node {NodeId}.",
                repositoryId,
                nodeId
            );
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                ex,
                "Deleting LFS data of repository {RepositoryId}, which no longer exists, on storage node {NodeId} failed; the next pass tries again.",
                repositoryId,
                nodeId
            );
        }
    }
}
