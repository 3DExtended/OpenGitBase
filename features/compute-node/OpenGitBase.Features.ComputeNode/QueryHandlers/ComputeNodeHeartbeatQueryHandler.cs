using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.ComputeNode.Contracts;
using OpenGitBase.Features.ComputeNode.Entities;

namespace OpenGitBase.Features.ComputeNode.QueryHandlers;

public sealed class ComputeNodeHeartbeatQueryHandler
    : IQueryHandler<ComputeNodeHeartbeatQuery, ComputeNodeDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IMapper _mapper;

    public ComputeNodeHeartbeatQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IMapper mapper
    )
    {
        _contextFactory = contextFactory;
        _mapper = mapper;
    }

    public async Task<Option<ComputeNodeDto>> RunQueryAsync(
        ComputeNodeHeartbeatQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var node = await context
            .Set<ComputeNodeEntity>()
            .FirstOrDefaultAsync(entity => entity.NodeId == query.NodeId, cancellationToken)
            .ConfigureAwait(false);
        if (node is null)
        {
            return Option<ComputeNodeDto>.None;
        }

        node.IsHealthy = true;
        node.LastHeartbeatAt = DateTimeOffset.UtcNow;
        node.RunningJobs = Math.Max(0, query.RunningJobs);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Option.From(_mapper.Map<ComputeNodeDto>(node));
    }
}
