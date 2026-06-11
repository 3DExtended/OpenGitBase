using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.PublicGitSshKey.Contracts;
using OpenGitBase.Features.PublicGitSshKey.Entities;

namespace OpenGitBase.Features.PublicGitSshKey.QueryHandlers;

public class GetPublicGitSshKeyQueryHandler
    : SingleModelQueryHandlerBase<
        GetPublicGitSshKeyQuery,
        PublicGitSshKeyDto,
        PublicGitSshKeyId,
        Guid,
        OpenGitBaseDbContext,
        Entities.PublicGitSshKeyEntity
    >
{
    public GetPublicGitSshKeyQueryHandler(
        IMapper mapper,
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
        : base(mapper, contextFactory) { }
}
