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
                ReplicationState = repository.ReplicationState.ToString(),
                PrimaryWatermark = repository.PrimaryWatermark,
                ReplicationEpoch = repository.ReplicationEpoch,
                WriteQuorumAvailable = routing.IsSome && routing.Get().WriteQuorumAvailable,
                Replicas = repository.Replicas
                    .Select(replica => new AdminRepositoryReplicaStatusResponse
                    {
                        StorageNodeId = replica.StorageNodeId,
                        Role = replica.Role.ToString(),
                        AppliedWatermark = replica.AppliedWatermark,
                        IsInSync = ReplicationSync.IsInSync(
                            replica.AppliedWatermark,
                            repository.PrimaryWatermark
                        ),
                        LastSyncedAt = replica.LastSyncedAt,
                    })
                    .ToList(),
            }
        );
    }
}
