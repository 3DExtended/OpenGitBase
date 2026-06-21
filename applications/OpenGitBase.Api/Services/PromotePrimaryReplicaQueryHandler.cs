using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Api.Services;

public sealed class PromotePrimaryReplicaQueryHandler
    : IQueryHandler<PromotePrimaryReplicaQuery, PromotePrimaryReplicaResult>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public PromotePrimaryReplicaQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<PromotePrimaryReplicaResult>> RunQueryAsync(
        PromotePrimaryReplicaQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var entity = await context
            .Set<RepositoryEntity>()
            .Include(repository => repository.Replicas)
            .FirstOrDefaultAsync(
                repository => repository.Id == query.RepositoryId.Value,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (entity is null || entity.PrimaryStorageNodeId is null || entity.Replicas.Count == 0)
        {
            return Option.From(new PromotePrimaryReplicaResult { Promoted = false });
        }

        var primaryNode = await context
            .Set<StorageNodeEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(node => node.Id == entity.PrimaryStorageNodeId, cancellationToken)
            .ConfigureAwait(false);

        if (primaryNode?.IsHealthy != false)
        {
            return Option.From(new PromotePrimaryReplicaResult { Promoted = false });
        }

        var candidate = entity
            .Replicas.Where(replica => replica.StorageNodeId != entity.PrimaryStorageNodeId)
            .OrderByDescending(replica => replica.AppliedWatermark)
            .ThenBy(replica => replica.StorageNodeId)
            .FirstOrDefault();

        if (candidate is null)
        {
            entity.ReplicationState = ReplicationState.Degraded;
            await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return Option.From(new PromotePrimaryReplicaResult { Promoted = false });
        }

        entity.ReplicationState = ReplicationState.Promoting;
        entity.ReplicationEpoch += 1;
        entity.PrimaryStorageNodeId = candidate.StorageNodeId;
        entity.StorageNodeId = candidate.StorageNodeId;

        foreach (var replica in entity.Replicas)
        {
            replica.Role = replica.StorageNodeId == candidate.StorageNodeId
                ? RepositoryReplicaRole.Primary
                : RepositoryReplicaRole.Replica;
        }

        entity.ReplicationState = ReplicationState.Rf3Healthy;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Option.From(
            new PromotePrimaryReplicaResult
            {
                Promoted = true,
                NewPrimaryStorageNodeId = candidate.StorageNodeId,
                ReplicationEpoch = entity.ReplicationEpoch,
            }
        );
    }
}
