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

        var storageNodeId = StorageNodeId.From(entity.StorageNodeId.Value);
        var storageNode = await _queryProcessor
            .RunQueryAsync(
                new GetStorageNodeQuery { ModelId = storageNodeId },
                cancellationToken
            )
            .ConfigureAwait(false);
        if (storageNode.IsNone)
        {
            return Option.From(
                DeleteRepositoryWithStorageResult.Failed("Assigned storage node was not found.")
            );
        }

        var node = storageNode.Get();
        if (!node.IsHealthy)
        {
            return Option.From(
                DeleteRepositoryWithStorageResult.Failed(
                    "Assigned storage node is unavailable."
                )
            );
        }

        var apiToken = await _queryProcessor
            .RunQueryAsync(
                new GetStorageNodeApiTokenQuery { StorageNodeId = storageNodeId },
                cancellationToken
            )
            .ConfigureAwait(false);
        if (apiToken.IsNone)
        {
            return Option.From(
                DeleteRepositoryWithStorageResult.Failed(
                    "Storage node API token is unavailable."
                )
            );
        }

        var deleteResult = await _storageProvisionerClient
            .DeleteRepositoryAsync(
                node,
                apiToken.Get(),
                entity.PhysicalPath,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (!deleteResult.Success)
        {
            return Option.From(
                DeleteRepositoryWithStorageResult.Failed(
                    $"Storage deletion failed: {deleteResult.Error}"
                )
            );
        }

        context.Remove(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Option.From(DeleteRepositoryWithStorageResult.Deleted());
    }
}
