using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Api.Models;
using OpenGitBase.Api.Services;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Api.Controllers;

[ApiController]
[Authorize(Roles = "admin")]
[Route("admin")]
public sealed class AdminRepositoryReplicationController : ControllerBase
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IQueryProcessor _queryProcessor;

    public AdminRepositoryReplicationController(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IQueryProcessor queryProcessor
    )
    {
        _contextFactory = contextFactory;
        _queryProcessor = queryProcessor;
    }

    [HttpGet("storage-nodes/replication-summary")]
    public async Task<
        ActionResult<IReadOnlyList<AdminStorageNodeReplicationSummaryResponse>>
    > GetStorageNodeSummary(CancellationToken cancellationToken)
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var nodes = await context
            .Set<StorageNodeEntity>()
            .AsNoTracking()
            .OrderBy(node => node.NodeId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var replicas = await context
            .Set<RepositoryReplicaEntity>()
            .AsNoTracking()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var memberNodeIds = replicas.Select(replica => replica.StorageNodeId).ToHashSet();

        var summaries = nodes
            .Select(node =>
            {
                var nodeReplicas = replicas.Where(replica =>
                    replica.StorageNodeId == node.Id
                );
                return new AdminStorageNodeReplicationSummaryResponse
                {
                    StorageNodeId = node.Id,
                    NodeId = node.NodeId,
                    PrimaryRepositoryCount = nodeReplicas.Count(replica =>
                        replica.Role == RepositoryReplicaRole.Primary
                    ),
                    ReplicaRepositoryCount = nodeReplicas.Count(replica =>
                        replica.Role == RepositoryReplicaRole.Replica
                    ),
                    IsSpare = node.IsHealthy && !memberNodeIds.Contains(node.Id),
                };
            })
            .ToList();

        return Ok(summaries);
    }

    [HttpGet("repositories")]
    public async Task<ActionResult<AdminRepositoryReplicationListResponse>> ListRepositories(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        [FromQuery] string? sort = null,
        [FromQuery] string? search = null,
        [FromQuery] string? attention = null,
        CancellationToken cancellationToken = default
    )
    {
        var result = await _queryProcessor
            .RunQueryAsync(
                new ListAdminRepositoryReplicationQuery
                {
                    Page = page,
                    PageSize = pageSize,
                    Sort = ReplicationAttention.ParseSort(sort),
                    Search = search,
                    Attention = ReplicationAttention.ParsePreset(attention),
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        if (result.IsNone)
        {
            return Ok(
                new AdminRepositoryReplicationListResponse
                {
                    Items = [],
                    TotalCount = 0,
                    Page = Math.Max(1, page),
                    PageSize = Math.Clamp(pageSize, 1, 100),
                }
            );
        }

        var payload = result.Get();
        return Ok(
            new AdminRepositoryReplicationListResponse
            {
                Items = payload.Items,
                TotalCount = payload.TotalCount,
                Page = payload.Page,
                PageSize = payload.PageSize,
            }
        );
    }

    [HttpGet("repositories/{repositoryId:guid}/replication")]
    public async Task<ActionResult<AdminRepositoryReplicationStatusResponse>> GetRepositoryStatus(
        Guid repositoryId,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var repository = await context
            .Set<RepositoryEntity>()
            .Include(entity => entity.Replicas)
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.Id == repositoryId, cancellationToken)
            .ConfigureAwait(false);

        if (repository is null)
        {
            return NotFound();
        }

        var nodeIds = repository.Replicas.Select(replica => replica.StorageNodeId).ToList();
        var nodes = new Dictionary<Guid, StorageNodeEntity>();
        if (
            nodeIds.Count > 0
            && context.Model.FindEntityType(typeof(StorageNodeEntity)) is not null
        )
        {
            nodes = await context
                .Set<StorageNodeEntity>()
                .AsNoTracking()
                .Where(node => nodeIds.Contains(node.Id))
                .ToDictionaryAsync(node => node.Id, cancellationToken)
                .ConfigureAwait(false);
        }

        var ownerSlug = await ResolveOwnerSlugAsync(context, repository.OwnerUserId, cancellationToken)
            .ConfigureAwait(false);

        var routing = await _queryProcessor
            .RunQueryAsync(
                new RepositoryReplicationRoutingQuery
                {
                    RepositoryId = RepositoryId.From(repositoryId),
                },
                cancellationToken
            )
            .ConfigureAwait(false);

        return Ok(
            new AdminRepositoryReplicationStatusResponse
            {
                RepositoryId = repository.Id,
                Name = repository.Name,
                Slug = repository.Slug,
                OwnerSlug = ownerSlug,
                ReplicationState = repository.ReplicationState.ToString(),
                PrimaryWatermark = repository.PrimaryWatermark,
                ReplicationEpoch = repository.ReplicationEpoch,
                WriteQuorumAvailable = routing.IsSome && routing.Get().WriteQuorumAvailable,
                Replicas = repository.Replicas
                    .Select(replica => new AdminRepositoryReplicaStatusResponse
                    {
                        StorageNodeId = replica.StorageNodeId,
                        NodeId = nodes.TryGetValue(replica.StorageNodeId, out var node)
                            ? node.NodeId
                            : string.Empty,
                        Role = replica.Role.ToString(),
                        AppliedWatermark = replica.AppliedWatermark,
                        ArtifactWatermark = replica.ArtifactWatermark,
                        IsInSync = replica.Role == RepositoryReplicaRole.EncryptedReplica
                            ? replica.ArtifactWatermark >= repository.PrimaryWatermark
                            : ReplicationSync.IsInSync(
                                replica.AppliedWatermark,
                                repository.PrimaryWatermark
                            ),
                        LastSyncedAt = replica.LastSyncedAt,
                    })
                    .ToList(),
            }
        );
    }

    private static async Task<string> ResolveOwnerSlugAsync(
        OpenGitBaseDbContext context,
        Guid ownerUserId,
        CancellationToken cancellationToken
    )
    {
        if (context.Model.FindEntityType(typeof(OpenGitBase.Features.Organization.Entities.OrganizationEntity)) != null)
        {
            var organization = await context
                .Set<OpenGitBase.Features.Organization.Entities.OrganizationEntity>()
                .AsNoTracking()
                .Where(org => org.Id == ownerUserId)
                .Select(org => org.Slug)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (!string.IsNullOrWhiteSpace(organization))
            {
                return organization;
            }
        }

        if (context.Model.FindEntityType(typeof(OpenGitBase.Features.Users.Entities.UserEntity)) != null)
        {
            var username = await context
                .Set<OpenGitBase.Features.Users.Entities.UserEntity>()
                .AsNoTracking()
                .Where(user => user.Id == ownerUserId)
                .Select(user => user.Username)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            return username ?? string.Empty;
        }

        return string.Empty;
    }
}
