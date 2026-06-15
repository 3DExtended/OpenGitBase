using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Features.StorageNode.QueryHandlers;

public sealed class GetStorageNodeByNodeIdQueryHandler
    : IQueryHandler<GetStorageNodeByNodeIdQuery, StorageNodeDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;
    private readonly IMapper _mapper;

    public GetStorageNodeByNodeIdQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IMapper mapper
    )
    {
        _contextFactory = contextFactory;
        _mapper = mapper;
    }

    public async Task<Option<StorageNodeDto>> RunQueryAsync(
        GetStorageNodeByNodeIdQuery query,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(query.NodeId))
        {
            return Option<StorageNodeDto>.None;
        }

        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var node = await context
            .Set<StorageNodeEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.NodeId == query.NodeId, cancellationToken)
            .ConfigureAwait(false);

        return node is null
            ? Option<StorageNodeDto>.None
            : Option.From(_mapper.Map<StorageNodeDto>(node));
    }
}
