using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.PublicGitSshKey.Contracts;
using OpenGitBase.Features.PublicGitSshKey.Entities;

namespace OpenGitBase.Features.PublicGitSshKey.QueryHandlers;

public class DeletePublicGitSshKeyQueryHandler
    : DeleteCommandHandlerBase<
        DeletePublicGitSshKeyQuery,
        PublicGitSshKeyDto,
        PublicGitSshKeyId,
        Guid,
        OpenGitBaseDbContext,
        Entities.PublicGitSshKeyEntity
    >
{
    public DeletePublicGitSshKeyQueryHandler(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
        : base(contextFactory) { }
}
