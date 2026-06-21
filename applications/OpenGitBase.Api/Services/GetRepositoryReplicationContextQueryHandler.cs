using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Api.Services;

public sealed class GetRepositoryReplicationContextQueryHandler
    : IQueryHandler<GetRepositoryReplicationContextQuery, RepositoryReplicationContextDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public GetRepositoryReplicationContextQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<RepositoryReplicationContextDto>> RunQueryAsync(
        GetRepositoryReplicationContextQuery query,
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

        if (entity is null)
        {
            return Option<RepositoryReplicationContextDto>.None;
        }

        var callerReplica = entity.Replicas.FirstOrDefault(replica =>
            replica.StorageNodeId == query.StorageNodeId.Value
        );
        if (callerReplica is null)
        {
            return Option<RepositoryReplicationContextDto>.None;
        }

        var nodeIds = entity.Replicas.Select(replica => replica.StorageNodeId).ToList();
        var nodes = await context
            .Set<StorageNodeEntity>()
            .AsNoTracking()
            .Where(node => nodeIds.Contains(node.Id))
            .ToDictionaryAsync(node => node.Id, cancellationToken)
            .ConfigureAwait(false);

        var peers = entity
            .Replicas.Select(replica =>
            {
                nodes.TryGetValue(replica.StorageNodeId, out var node);
                return new RepositoryReplicationPeerDto
                {
                    StorageNodeId = replica.StorageNodeId,
                    InternalHost = node?.InternalHost ?? string.Empty,
                    InternalHttpPort = node?.InternalHttpPort ?? 0,
                    Role = replica.Role.ToString(),
                    IsHealthy = node?.IsHealthy ?? false,
                };
            })
            .ToList();

        return Option.From(
            new RepositoryReplicationContextDto
            {
                RepositoryId = entity.Id,
                ReplicationEpoch = entity.ReplicationEpoch,
                PrimaryWatermark = entity.PrimaryWatermark,
                IsPrimary = callerReplica.Role == RepositoryReplicaRole.Primary,
                PhysicalPath = entity.PhysicalPath,
                Peers = peers,
            }
        );
    }
}
