using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Services;

public sealed class CreateRepositoryWithStorageQueryHandler
    : IQueryHandler<CreateRepositoryWithStorageQuery, CreateRepositoryWithStorageResult>
{
    private readonly IQueryProcessor _queryProcessor;
    private readonly IStorageProvisionerClient _storageProvisionerClient;
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IMapper _mapper;
    private readonly RepositoryStorageQuotaOptions _quotaOptions;

    public CreateRepositoryWithStorageQueryHandler(
        IQueryProcessor queryProcessor,
        IStorageProvisionerClient storageProvisionerClient,
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IMapper mapper,
        RepositoryStorageQuotaOptions quotaOptions
    )
    {
        _queryProcessor = queryProcessor;
        _storageProvisionerClient = storageProvisionerClient;
        _contextFactory = contextFactory;
        _mapper = mapper;
        _quotaOptions = quotaOptions;
    }

    public async Task<Option<CreateRepositoryWithStorageResult>> RunQueryAsync(
        CreateRepositoryWithStorageQuery query,
        CancellationToken cancellationToken
    )
    {
        var healthyNodes = await _queryProcessor
            .RunQueryAsync(new ListHealthyStorageNodesQuery(), cancellationToken)
            .ConfigureAwait(false);
        var nodes = healthyNodes.IsSome ? healthyNodes.Get() : Array.Empty<StorageNodeDto>();
        var selectedNode = StorageNodeSelection.SelectBestNode(nodes);
        if (selectedNode is null)
        {
            return Option.From(
                CreateRepositoryWithStorageResult.Failed("No healthy storage node is available.")
            );
        }

        var apiToken = await _queryProcessor
            .RunQueryAsync(
                new GetStorageNodeApiTokenQuery { StorageNodeId = selectedNode.Id },
                cancellationToken
            )
            .ConfigureAwait(false);
        if (apiToken.IsNone)
        {
            return Option.From(
                CreateRepositoryWithStorageResult.Failed(
                    "Storage node API token is unavailable."
                )
            );
        }

        var repositoryId = Guid.NewGuid();
        var physicalPath = $"/srv/git/{repositoryId}.git";
        var receiveMaxBytes = _quotaOptions.Enabled ? _quotaOptions.MaxFileBytes : 0L;
        var provisionResult = await _storageProvisionerClient
            .ProvisionRepositoryAsync(
                selectedNode,
                apiToken.Get(),
                physicalPath,
                receiveMaxBytes,
                cancellationToken
            )
            .ConfigureAwait(false);
        if (!provisionResult.Success)
        {
            return Option.From(
                CreateRepositoryWithStorageResult.Failed(
                    $"Storage provisioning failed: {provisionResult.Error}"
                )
            );
        }

        try
        {
            var model = query.ModelToCreate;
            model.Id = RepositoryId.From(repositoryId);
            model.PhysicalPath = physicalPath;
            model.StorageNodeId = selectedNode.Id;

            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);
            var entity = _mapper.Map<RepositoryEntity>(model);
            entity.Id = repositoryId;
            entity.StorageNodeId = selectedNode.Id.Value;
            context.Set<RepositoryEntity>().Add(entity);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            return Option.From(
                CreateRepositoryWithStorageResult.Created(RepositoryId.From(repositoryId))
            );
        }
        catch (Exception)
        {
            await _storageProvisionerClient
                .DeleteRepositoryAsync(
                    selectedNode,
                    apiToken.Get(),
                    physicalPath,
                    CancellationToken.None
                )
                .ConfigureAwait(false);
            throw;
        }
    }
}
