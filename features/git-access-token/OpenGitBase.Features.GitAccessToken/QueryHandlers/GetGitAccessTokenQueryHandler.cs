using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.GitAccessToken.Contracts;
using OpenGitBase.Features.GitAccessToken.Entities;

namespace OpenGitBase.Features.GitAccessToken.QueryHandlers;

public class GetGitAccessTokenQueryHandler
    : SingleModelQueryHandlerBase<
        GetGitAccessTokenQuery,
        GitAccessTokenDto,
        GitAccessTokenId,
        Guid,
        OpenGitBaseDbContext,
        Entities.GitAccessTokenEntity
    >
{
    public GetGitAccessTokenQueryHandler(
        IMapper mapper,
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
        : base(mapper, contextFactory)
    {
    }
}
