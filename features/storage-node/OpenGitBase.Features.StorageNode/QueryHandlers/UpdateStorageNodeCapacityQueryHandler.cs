using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Features.StorageNode.QueryHandlers;

public sealed class UpdateStorageNodeCapacityQueryHandler
    : IQueryHandler<UpdateStorageNodeCapacityQuery, StorageNodeDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IMapper _mapper;

    public UpdateStorageNodeCapacityQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IMapper mapper
    )
    {
        _contextFactory = contextFactory;
        _mapper = mapper;
    }

    public async Task<Option<StorageNodeDto>> RunQueryAsync(
        UpdateStorageNodeCapacityQuery query,
        CancellationToken cancellationToken
    )
    {
        if (query.MaxBytes < 0)
        {
            return Option<StorageNodeDto>.None;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var node = await context
            .Set<StorageNodeEntity>()
            .FirstOrDefaultAsync(entity => entity.Id == query.StorageNodeId.Value, cancellationToken)
            .ConfigureAwait(false);

        if (node is null)
        {
            return Option<StorageNodeDto>.None;
        }

        node.MaxBytes = query.MaxBytes;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Option.From(_mapper.Map<StorageNodeDto>(node));
    }
}
