using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Features.StorageNode.QueryHandlers;

public class DeleteStorageNodeQueryHandler
    : DeleteCommandHandlerBase<
        DeleteStorageNodeQuery,
        StorageNodeDto,
        StorageNodeId,
        Guid,
        OpenGitBaseDbContext,
        Entities.StorageNodeEntity
    >
{
    public DeleteStorageNodeQueryHandler(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
        : base(contextFactory)
    {
    }
}
