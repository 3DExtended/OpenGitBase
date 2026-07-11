using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.ComputeNode.Contracts;
using OpenGitBase.Features.ComputeNode.Entities;

namespace OpenGitBase.Features.ComputeNode.QueryHandlers;

public sealed class ListComputeNodesQueryHandler
    : IQueryHandler<ListComputeNodesQuery, IReadOnlyList<ComputeNodeDto>>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IMapper _mapper;

    public ListComputeNodesQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IMapper mapper
    )
    {
        _contextFactory = contextFactory;
        _mapper = mapper;
    }

    public async Task<Option<IReadOnlyList<ComputeNodeDto>>> RunQueryAsync(
        ListComputeNodesQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var nodesQuery = context.Set<ComputeNodeEntity>().AsQueryable();
        if (query.OrganizationId.HasValue)
        {
            nodesQuery = nodesQuery.Where(entity => entity.OrganizationId == query.OrganizationId.Value);
        }

        var nodes = await nodesQuery
            .OrderBy(entity => entity.NodeId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return Option.From((IReadOnlyList<ComputeNodeDto>)nodes.Select(_mapper.Map<ComputeNodeDto>).ToList());
    }
}
