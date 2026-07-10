using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Features.StorageNode.QueryHandlers;

public sealed class ListOrganizationStorageNodesQueryHandler
    : IQueryHandler<ListOrganizationStorageNodesQuery, IReadOnlyList<StorageNodeDto>>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IMapper _mapper;

    public ListOrganizationStorageNodesQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IMapper mapper
    )
    {
        _contextFactory = contextFactory;
        _mapper = mapper;
    }

    public async Task<Option<IReadOnlyList<StorageNodeDto>>> RunQueryAsync(
        ListOrganizationStorageNodesQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var nodes = await context
            .Set<StorageNodeEntity>()
            .AsNoTracking()
            .Where(node => node.OwnerOrganizationId == query.OrganizationId)
            .OrderByDescending(node => node.RegisteredAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Option.From<IReadOnlyList<StorageNodeDto>>(
            nodes.Select(node => _mapper.Map<StorageNodeDto>(node)).ToList()
        );
    }
}
