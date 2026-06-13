using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Features.Repository.QueryHandlers;

public class ListRepositoryQueryHandler
    : ListOfModelQueryHandlerBase<
        ListRepositoryQuery,
        RepositoryDto,
        RepositoryId,
        Guid,
        OpenGitBaseDbContext,
        Entities.RepositoryEntity
    >
{
    public ListRepositoryQueryHandler(
        IMapper mapper,
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
        : base(mapper, contextFactory) { }

    protected override IQueryable<RepositoryEntity> AddWhere(
        IQueryable<RepositoryEntity> queryable,
        ListRepositoryQuery query
    )
    {
        return queryable.Where(x => x.OwnerUserId == query.OwnerUserId.Value);
    }
}
