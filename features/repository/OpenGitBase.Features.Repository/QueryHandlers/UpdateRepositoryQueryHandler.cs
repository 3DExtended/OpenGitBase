using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Features.Repository.QueryHandlers;

public class UpdateRepositoryQueryHandler
    : UpdateCommandHandlerBase<
        UpdateRepositoryQuery,
        RepositoryDto,
        RepositoryId,
        Guid,
        OpenGitBaseDbContext,
        Entities.RepositoryEntity
    >
{
    public UpdateRepositoryQueryHandler(
        IMapper mapper,
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
        : base(mapper, contextFactory) { }
}
