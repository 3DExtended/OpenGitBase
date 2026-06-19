using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.GitAccessToken.Contracts;
using OpenGitBase.Features.GitAccessToken.Entities;

namespace OpenGitBase.Features.GitAccessToken.QueryHandlers;

public class ListGitAccessTokenQueryHandler
    : ListOfModelQueryHandlerBase<
        ListGitAccessTokenQuery,
        GitAccessTokenDto,
        GitAccessTokenId,
        Guid,
        OpenGitBaseDbContext,
        Entities.GitAccessTokenEntity
    >
{
    public ListGitAccessTokenQueryHandler(
        IMapper mapper,
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
        : base(mapper, contextFactory) { }

    protected override IQueryable<GitAccessTokenEntity> AddWhere(
        IQueryable<GitAccessTokenEntity> queryable,
        ListGitAccessTokenQuery query
    )
    {
        if (query.ForUser.IsSome)
        {
            queryable = queryable.Where(entity => entity.OwnerUserId == query.ForUser.Get().Value);
        }

        return queryable.OrderByDescending(entity => entity.Id);
    }
}
