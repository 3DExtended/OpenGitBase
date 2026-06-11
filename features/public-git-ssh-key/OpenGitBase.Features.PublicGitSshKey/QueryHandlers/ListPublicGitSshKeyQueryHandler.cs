using System.Runtime.CompilerServices;
using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.PublicGitSshKey.Contracts;
using OpenGitBase.Features.PublicGitSshKey.Entities;

namespace OpenGitBase.Features.PublicGitSshKey.QueryHandlers;

public class ListPublicGitSshKeyQueryHandler
    : ListOfModelQueryHandlerBase<
        ListPublicGitSshKeyQuery,
        PublicGitSshKeyDto,
        PublicGitSshKeyId,
        Guid,
        OpenGitBaseDbContext,
        Entities.PublicGitSshKeyEntity
    >
{
    public ListPublicGitSshKeyQueryHandler(
        IMapper mapper,
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
        : base(mapper, contextFactory) { }

    protected override IQueryable<PublicGitSshKeyEntity> AddWhere(
        IQueryable<PublicGitSshKeyEntity> queryable,
        ListPublicGitSshKeyQuery query
    )
    {
        if (query.ForUser.IsNone)
        {
            return queryable;
        }

        return queryable.Where(entity => query.ForUser.Get().Value == entity.OwnerUserId);
    }
}
