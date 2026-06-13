using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Features.Repository.QueryHandlers;

public class DeleteRepositoryQueryHandler
    : DeleteCommandHandlerBase<
        DeleteRepositoryQuery,
        RepositoryDto,
        RepositoryId,
        Guid,
        OpenGitBaseDbContext,
        Entities.RepositoryEntity
    >
{
    public DeleteRepositoryQueryHandler(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
        : base(contextFactory) { }
}
