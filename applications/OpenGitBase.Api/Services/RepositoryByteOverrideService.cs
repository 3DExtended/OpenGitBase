using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Api.Services;

public sealed class RepositoryByteOverrideService : IRepositoryByteOverrideService
{
    public const int MinimumOrgContributedNodes = 4;

    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IQueryProcessor _queryProcessor;
    private readonly StorageNodeOptions _storageNodeOptions;

    public RepositoryByteOverrideService(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IQueryProcessor queryProcessor,
        StorageNodeOptions storageNodeOptions
    )
    {
        _contextFactory = contextFactory;
        _queryProcessor = queryProcessor;
        _storageNodeOptions = storageNodeOptions;
    }

    public async Task<RepositoryByteOverrideEligibilityDto> EvaluateAsync(
        RepositoryEntity repository,
        CancellationToken cancellationToken
    )
    {
        var dto = new RepositoryByteOverrideEligibilityDto
        {
            CurrentOverride = repository.MaxBytesOverride,
        };

        var organizationResult = await _queryProcessor
            .RunQueryAsync(
                new GetOrganizationQuery
                {
                    ModelId = OrganizationId.From(repository.OwnerUserId),
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (organizationResult.IsNone)
        {
            dto.Reason = "Repository is not organization-owned.";
            return dto;
        }

        var organizationId = repository.OwnerUserId;
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var cutoff = DateTimeOffset.UtcNow.AddSeconds(-_storageNodeOptions.MissedHeartbeatThresholdSeconds);
        var orgNodes = await context
            .Set<StorageNodeEntity>()
            .AsNoTracking()
            .Where(node => node.OwnerOrganizationId == organizationId && node.IsHealthy)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        orgNodes = orgNodes
            .Where(node => node.LastHeartbeatAt != null && node.LastHeartbeatAt >= cutoff)
            .ToList();

        dto.OrgContributedNodeCount = orgNodes.Count;
        dto.MaxAllowedOverride = orgNodes.Where(node => node.MaxBytes > 0).Sum(node => node.MaxBytes);

        if (orgNodes.Count <= 3)
        {
            dto.Reason = "Organization must operate more than three healthy storage nodes.";
            return dto;
        }

        var replicaNodeIds = repository
            .Replicas.Select(replica => replica.StorageNodeId)
            .Distinct()
            .ToList();
        if (replicaNodeIds.Count == 0)
        {
            dto.Reason = "Repository replicas are not provisioned.";
            return dto;
        }

        var replicaNodes = await context
            .Set<StorageNodeEntity>()
            .AsNoTracking()
            .Where(node => replicaNodeIds.Contains(node.Id))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (replicaNodes.Count != replicaNodeIds.Count)
        {
            dto.Reason = "Replica storage nodes are unavailable.";
            return dto;
        }

        if (replicaNodes.Any(node => node.OwnerOrganizationId != organizationId))
        {
            dto.Reason =
                "All repository copies must reside on organization-owned nodes before setting an override.";
            return dto;
        }

        if (!HasFullReplicaSet(repository.Replicas))
        {
            dto.Reason = "Repository is not fully replicated.";
            return dto;
        }

        dto.Eligible = true;
        dto.Reason = "Eligible for per-repository byte override.";
        return dto;
    }

    private static bool HasFullReplicaSet(IEnumerable<RepositoryReplicaEntity> replicas)
    {
        var hasPrimary = replicas.Any(replica => replica.Role == RepositoryReplicaRole.Primary);
        var hasReadReplica =
            replicas.Any(replica => replica.Role == RepositoryReplicaRole.ReadReplica) || hasPrimary;
        var encryptedReplicaCount = replicas.Count(replica =>
            replica.Role == RepositoryReplicaRole.EncryptedReplica
        );

        return hasPrimary && hasReadReplica && encryptedReplicaCount >= 2;
    }
}
