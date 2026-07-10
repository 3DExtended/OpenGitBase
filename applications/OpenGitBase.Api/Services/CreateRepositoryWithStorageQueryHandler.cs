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
    private readonly IRepositoryKeyService _repositoryKeyService;
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IMapper _mapper;
    private readonly RepositoryStorageQuotaOptions _quotaOptions;

    public CreateRepositoryWithStorageQueryHandler(
        IQueryProcessor queryProcessor,
        IStorageProvisionerClient storageProvisionerClient,
        IRepositoryKeyService repositoryKeyService,
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IMapper mapper,
        RepositoryStorageQuotaOptions quotaOptions
    )
    {
        _queryProcessor = queryProcessor;
        _storageProvisionerClient = storageProvisionerClient;
        _repositoryKeyService = repositoryKeyService;
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
        var replicaSet = ReplicaSetPlanner.SelectReplicaSet(nodes);
        if (replicaSet is null)
        {
            return Option.From(
                CreateRepositoryWithStorageResult.Failed(
                    "At least three healthy storage nodes are required."
                )
            );
        }

        var nodeTokens = new Dictionary<StorageNodeId, string>();
        foreach (var node in replicaSet.AllNodes)
        {
            var apiToken = await _queryProcessor
                .RunQueryAsync(
                    new GetStorageNodeApiTokenQuery { StorageNodeId = node.Id },
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

            nodeTokens[node.Id] = apiToken.Get();
        }

        var repositoryId = Guid.NewGuid();
        var physicalPath = $"/srv/git/{repositoryId}.git";
        var receiveMaxBytes = _quotaOptions.Enabled ? _quotaOptions.MaxFileBytes : 0L;
        var provisionedNodes = new List<(StorageNodeDto Node, RepositoryReplicaRole Role)>();

        foreach (var (node, role) in replicaSet.ProvisionTargets)
        {
            var provisionResult = await _storageProvisionerClient
                .ProvisionRepositoryAsync(
                    node,
                    nodeTokens[node.Id],
                    physicalPath,
                    receiveMaxBytes,
                    role.ToString(),
                    cancellationToken
                )
                .ConfigureAwait(false);
            if (!provisionResult.Success)
            {
                await RollBackProvisionedAsync(
                    provisionedNodes,
                    nodeTokens,
                    physicalPath,
                    cancellationToken
                ).ConfigureAwait(false);
                return Option.From(
                    CreateRepositoryWithStorageResult.Failed(
                        $"Storage provisioning failed: {provisionResult.Error}"
                    )
                );
            }

            provisionedNodes.Add((node, role));
        }

        try
        {
            var model = query.ModelToCreate;
            model.Id = RepositoryId.From(repositoryId);
            model.PhysicalPath = physicalPath;
            model.StorageNodeId = replicaSet.Primary.Id;

            await using var context = await _contextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);
            var entity = _mapper.Map<RepositoryEntity>(model);
            entity.Id = repositoryId;
            entity.StorageNodeId = replicaSet.Primary.Id.Value;
            entity.PrimaryStorageNodeId = replicaSet.Primary.Id.Value;
            entity.ReadReplicaStorageNodeId = replicaSet.ReadReplica.Id.Value;
            entity.ReplicationEpoch = 1;
            entity.PrimaryWatermark = 0;
            entity.ReplicationState = ReplicationState.Rf4Healthy;
            entity.Replicas =
            [
                new RepositoryReplicaEntity
                {
                    RepositoryId = repositoryId,
                    StorageNodeId = replicaSet.Primary.Id.Value,
                    Role = RepositoryReplicaRole.Primary,
                },
                new RepositoryReplicaEntity
                {
                    RepositoryId = repositoryId,
                    StorageNodeId = replicaSet.ReadReplica.Id.Value,
                    Role = RepositoryReplicaRole.ReadReplica,
                },
                new RepositoryReplicaEntity
                {
                    RepositoryId = repositoryId,
                    StorageNodeId = replicaSet.EncryptedReplicaA.Id.Value,
                    Role = RepositoryReplicaRole.EncryptedReplica,
                },
                new RepositoryReplicaEntity
                {
                    RepositoryId = repositoryId,
                    StorageNodeId = replicaSet.EncryptedReplicaB.Id.Value,
                    Role = RepositoryReplicaRole.EncryptedReplica,
                },
            ];
            context.Set<RepositoryEntity>().Add(entity);
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await _repositoryKeyService
                .GenerateAndStoreKeyAsync(repositoryId, cancellationToken)
                .ConfigureAwait(false);

            return Option.From(
                CreateRepositoryWithStorageResult.Created(RepositoryId.From(repositoryId))
            );
        }
        catch (Exception)
        {
            await RollBackProvisionedAsync(
                provisionedNodes,
                nodeTokens,
                physicalPath,
                CancellationToken.None
            ).ConfigureAwait(false);
            throw;
        }
    }

    private async Task RollBackProvisionedAsync(
        IReadOnlyList<(StorageNodeDto Node, RepositoryReplicaRole Role)> provisionedNodes,
        IReadOnlyDictionary<StorageNodeId, string> nodeTokens,
        string physicalPath,
        CancellationToken cancellationToken
    )
    {
        foreach (var (node, _) in provisionedNodes)
        {
            if (!nodeTokens.TryGetValue(node.Id, out var token))
            {
                continue;
            }

            await _storageProvisionerClient
                .DeleteRepositoryAsync(node, token, physicalPath, cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
