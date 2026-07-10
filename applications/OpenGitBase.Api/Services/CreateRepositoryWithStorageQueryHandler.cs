using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Contracts;

namespace OpenGitBase.Api.Services;

public sealed class CreateRepositoryWithStorageQueryHandler
    : IQueryHandler<CreateRepositoryWithStorageQuery, CreateRepositoryWithStorageResult>
{
    private const long InitialRepositoryBytesEstimate = 4096;

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
        var plannerRequest = await BuildPlannerRequestAsync(query, cancellationToken)
            .ConfigureAwait(false);
        var replicaSet = ReplicaSetPlanner.SelectReplicaSet(
            plannerRequest with { HealthyNodes = nodes }
        );
        if (replicaSet is null)
        {
            return Option.From(
                CreateRepositoryWithStorageResult.Failed(
                    "Unable to assign a healthy replica set with sufficient node capacity."
                )
            );
        }

        foreach (var node in replicaSet.AllNodes)
        {
            if (!StorageNodeCapacity.HasCapacity(node, InitialRepositoryBytesEstimate))
            {
                return Option.From(
                    CreateRepositoryWithStorageResult.Failed(
                        $"Storage node {node.NodeId} does not have sufficient capacity."
                    )
                );
            }
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
            entity.PlacementPolicy = query.ModelToCreate.PlacementPolicy;
            entity.Replicas = BuildReplicaEntities(repositoryId, replicaSet);
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

    private static List<RepositoryReplicaEntity> BuildReplicaEntities(
        Guid repositoryId,
        ReplicaSetSelection replicaSet
    )
    {
        var replicas = new List<RepositoryReplicaEntity>
        {
            new()
            {
                RepositoryId = repositoryId,
                StorageNodeId = replicaSet.Primary.Id.Value,
                Role = RepositoryReplicaRole.Primary,
            },
        };

        if (replicaSet.ReadReplica.Id != replicaSet.Primary.Id)
        {
            replicas.Add(
                new RepositoryReplicaEntity
                {
                    RepositoryId = repositoryId,
                    StorageNodeId = replicaSet.ReadReplica.Id.Value,
                    Role = RepositoryReplicaRole.ReadReplica,
                }
            );
        }

        replicas.Add(
            new RepositoryReplicaEntity
            {
                RepositoryId = repositoryId,
                StorageNodeId = replicaSet.EncryptedReplicaA.Id.Value,
                Role = RepositoryReplicaRole.EncryptedReplica,
            }
        );
        replicas.Add(
            new RepositoryReplicaEntity
            {
                RepositoryId = repositoryId,
                StorageNodeId = replicaSet.EncryptedReplicaB.Id.Value,
                Role = RepositoryReplicaRole.EncryptedReplica,
            }
        );
        return replicas;
    }

    private async Task<ReplicaSetPlannerRequest> BuildPlannerRequestAsync(
        CreateRepositoryWithStorageQuery query,
        CancellationToken cancellationToken
    )
    {
        var ownerId = query.ModelToCreate.OwnerUserId.Value;
        var organizationResult = await _queryProcessor
            .RunQueryAsync(
                new GetOrganizationQuery { ModelId = OrganizationId.From(ownerId) },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (organizationResult.IsNone)
        {
            var requiredBytes = query.ModelToCreate.MaxBytesOverride is > 0
                ? query.ModelToCreate.MaxBytesOverride.Value
                : InitialRepositoryBytesEstimate;
            return new ReplicaSetPlannerRequest(
                HealthyNodes: Array.Empty<StorageNodeDto>(),
                RequiredBytesPerNode: requiredBytes
            );
        }

        var settingsResult = await _queryProcessor
            .RunQueryAsync(
                new GetOrganizationStorageSettingsQuery
                {
                    OrganizationId = OrganizationId.From(ownerId),
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        var settings = settingsResult.IsSome
            ? settingsResult.Get()
            : new OrganizationStorageSettingsDto
            {
                OrganizationId = ownerId,
            };

        var placementPolicy =
            query.ModelToCreate.PlacementPolicy ?? settings.DefaultPlacementPolicy;
        if (placementPolicy == PlacementPolicy.Inherit)
        {
            placementPolicy = settings.DefaultPlacementPolicy == PlacementPolicy.Inherit
                ? PlacementPolicy.PlatformDefault
                : settings.DefaultPlacementPolicy;
        }

        return new ReplicaSetPlannerRequest(
            HealthyNodes: Array.Empty<StorageNodeDto>(),
            OwnerOrganizationId: ownerId,
            PlacementPolicy: placementPolicy,
            SelfHostPreference: settings.DefaultSelfHostPreference,
            RequiredBytesPerNode: query.ModelToCreate.MaxBytesOverride is > 0
                ? query.ModelToCreate.MaxBytesOverride.Value
                : InitialRepositoryBytesEstimate
        );
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
