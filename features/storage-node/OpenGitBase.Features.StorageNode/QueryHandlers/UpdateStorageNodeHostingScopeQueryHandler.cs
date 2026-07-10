using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Features.StorageNode.QueryHandlers;

public sealed class UpdateStorageNodeHostingScopeQueryHandler
    : IQueryHandler<UpdateStorageNodeHostingScopeQuery, StorageNodeDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IMapper _mapper;

    public UpdateStorageNodeHostingScopeQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IMapper mapper
    )
    {
        _contextFactory = contextFactory;
        _mapper = mapper;
    }

    public async Task<Option<StorageNodeDto>> RunQueryAsync(
        UpdateStorageNodeHostingScopeQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var node = await context
            .Set<StorageNodeEntity>()
            .FirstOrDefaultAsync(entity => entity.Id == query.StorageNodeId.Value, cancellationToken)
            .ConfigureAwait(false);

        if (node is null || node.OwnerOrganizationId is null)
        {
            return Option<StorageNodeDto>.None;
        }

        node.HostingScope = query.HostingScope;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Option.From(_mapper.Map<StorageNodeDto>(node));
    }
}
