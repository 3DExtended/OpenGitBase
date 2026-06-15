using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Features.StorageNode.QueryHandlers;

public sealed class StorageNodeHeartbeatQueryHandler
    : IQueryHandler<StorageNodeHeartbeatQuery, StorageNodeHeartbeatResult>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public StorageNodeHeartbeatQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<StorageNodeHeartbeatResult>> RunQueryAsync(
        StorageNodeHeartbeatQuery query,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(query.NodeId))
        {
            return Option<StorageNodeHeartbeatResult>.None;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var node = await context
            .Set<StorageNodeEntity>()
            .FirstOrDefaultAsync(entity => entity.NodeId == query.NodeId, cancellationToken)
            .ConfigureAwait(false);

        if (node is null)
        {
            return Option<StorageNodeHeartbeatResult>.None;
        }

        node.FreeBytesAvailable = query.FreeBytesAvailable;
        node.TotalBytesAvailable = query.TotalBytesAvailable;
        node.LastHeartbeatAt = DateTimeOffset.UtcNow;
        node.IsHealthy = true;

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Option.From(new StorageNodeHeartbeatResult { Acknowledged = true });
    }
}
