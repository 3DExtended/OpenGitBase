using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Api.Services;

public sealed class RepositoryReplicationRoutingQueryHandler
    : IQueryHandler<RepositoryReplicationRoutingQuery, RepositoryReplicationRoutingDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public RepositoryReplicationRoutingQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<RepositoryReplicationRoutingDto>> RunQueryAsync(
        RepositoryReplicationRoutingQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var entity = await context
            .Set<RepositoryEntity>()
            .Include(repository => repository.Replicas)
            .AsNoTracking()
            .FirstOrDefaultAsync(
                repository => repository.Id == query.RepositoryId.Value,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (entity is null || entity.PrimaryStorageNodeId is null)
        {
            if (entity?.StorageNodeId is null)
            {
                return Option<RepositoryReplicationRoutingDto>.None;
            }

            var legacyNode = await context
                .Set<StorageNodeEntity>()
                .AsNoTracking()
                .FirstOrDefaultAsync(node => node.Id == entity.StorageNodeId, cancellationToken)
                .ConfigureAwait(false);

            if (legacyNode is null)
            {
                return Option<RepositoryReplicationRoutingDto>.None;
            }

            var legacyTarget = new RepositoryRoutingTargetDto
            {
                StorageNodeId = legacyNode.Id,
                InternalHost = legacyNode.InternalHost,
                InternalSshPort = legacyNode.InternalSshPort,
                InternalGitHttpPort = legacyNode.InternalGitHttpPort,
                Role = nameof(RepositoryReplicaRole.Primary),
                IsHealthy = legacyNode.IsHealthy,
                IsInSync = true,
                IsPrimary = true,
            };

            return Option.From(
                new RepositoryReplicationRoutingDto
                {
                    ReplicationEpoch = entity.ReplicationEpoch,
                    WriteQuorumAvailable = legacyNode.IsHealthy,
                    Targets = [legacyTarget],
                }
            );
        }

        var nodeIds = entity.Replicas.Select(replica => replica.StorageNodeId).ToList();
        var nodes = await context
            .Set<StorageNodeEntity>()
            .AsNoTracking()
            .Where(node => nodeIds.Contains(node.Id))
            .ToDictionaryAsync(node => node.Id, cancellationToken)
            .ConfigureAwait(false);

        var healthyCount = entity.Replicas.Count(replica =>
            nodes.TryGetValue(replica.StorageNodeId, out var node) && node.IsHealthy
        );

        var targets = entity
            .Replicas.Select(replica =>
            {
                nodes.TryGetValue(replica.StorageNodeId, out var node);
                var inSync = node?.IsHealthy == true
                    && ReplicationSync.IsInSync(
                        replica.AppliedWatermark,
                        entity.PrimaryWatermark
                    );
                return new RepositoryRoutingTargetDto
                {
                    StorageNodeId = replica.StorageNodeId,
                    InternalHost = node?.InternalHost ?? string.Empty,
                    InternalSshPort = node?.InternalSshPort ?? 22,
                    InternalGitHttpPort = node?.InternalGitHttpPort ?? 8082,
                    Role = replica.Role.ToString(),
                    IsHealthy = node?.IsHealthy ?? false,
                    IsInSync = inSync,
                    IsPrimary = replica.StorageNodeId == entity.PrimaryStorageNodeId,
                };
            })
            .ToList();

        return Option.From(
            new RepositoryReplicationRoutingDto
            {
                ReplicationEpoch = entity.ReplicationEpoch,
                WriteQuorumAvailable = healthyCount >= 2,
                Targets = targets,
            }
        );
    }
}
