using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Features.StorageNode.QueryHandlers;

public class UpdateStorageNodeQueryHandler
    : UpdateCommandHandlerBase<
        UpdateStorageNodeQuery,
        StorageNodeDto,
        StorageNodeId,
        Guid,
        OpenGitBaseDbContext,
        Entities.StorageNodeEntity
    >
{
    public UpdateStorageNodeQueryHandler(
        IMapper mapper,
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
        : base(mapper, contextFactory)
    {
    }
}
