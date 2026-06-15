using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Options;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Features.StorageNode.QueryHandlers;

public sealed class ListHealthyStorageNodesQueryHandler
    : IQueryHandler<ListHealthyStorageNodesQuery, IReadOnlyList<StorageNodeDto>>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IMapper _mapper;
    private readonly StorageNodeOptions _options;

    public ListHealthyStorageNodesQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IMapper mapper,
        StorageNodeOptions options
    )
    {
        _contextFactory = contextFactory;
        _mapper = mapper;
        _options = options;
    }

    public async Task<Option<IReadOnlyList<StorageNodeDto>>> RunQueryAsync(
        ListHealthyStorageNodesQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        await MarkStaleNodesUnhealthyAsync(context, cancellationToken).ConfigureAwait(false);

        var nodes = await context
            .Set<StorageNodeEntity>()
            .AsNoTracking()
            .Where(node => node.IsHealthy)
            .OrderByDescending(node => node.FreeBytesAvailable)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Option.From<IReadOnlyList<StorageNodeDto>>(
            nodes.Select(node => _mapper.Map<StorageNodeDto>(node)).ToList()
        );
    }

    internal async Task MarkStaleNodesUnhealthyAsync(
        OpenGitBaseDbContext context,
        CancellationToken cancellationToken
    )
    {
        var cutoff = DateTimeOffset.UtcNow.AddSeconds(-_options.MissedHeartbeatThresholdSeconds);
        var healthyNodes = await context
            .Set<StorageNodeEntity>()
            .Where(node => node.IsHealthy)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var staleNodes = healthyNodes
            .Where(node => node.LastHeartbeatAt is null || node.LastHeartbeatAt < cutoff)
            .ToList();

        if (staleNodes.Count == 0)
        {
            return;
        }

        foreach (var node in staleNodes)
        {
            node.IsHealthy = false;
        }

        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
