using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Features.Repository.QueryHandlers;

public class CreateRepositoryQueryHandler
    : CreateQueryHandlerBase<
        CreateRepositoryQuery,
        RepositoryDto,
        RepositoryId,
        Guid,
        OpenGitBaseDbContext,
        Entities.RepositoryEntity
    >
{
    public CreateRepositoryQueryHandler(
        IMapper mapper,
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
        : base(mapper, contextFactory) { }
}
