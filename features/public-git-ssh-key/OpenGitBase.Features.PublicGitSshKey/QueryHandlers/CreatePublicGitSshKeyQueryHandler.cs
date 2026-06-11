using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.PublicGitSshKey.Contracts;
using OpenGitBase.Features.PublicGitSshKey.Entities;

namespace OpenGitBase.Features.PublicGitSshKey.QueryHandlers;

public class CreatePublicGitSshKeyQueryHandler
    : CreateQueryHandlerBase<
        CreatePublicGitSshKeyQuery,
        PublicGitSshKeyDto,
        PublicGitSshKeyId,
        Guid,
        OpenGitBaseDbContext,
        Entities.PublicGitSshKeyEntity
    >
{
    public CreatePublicGitSshKeyQueryHandler(
        IMapper mapper,
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
        : base(mapper, contextFactory) { }
}
