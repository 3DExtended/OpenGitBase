using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.Entities;

namespace OpenGitBase.Features.Organization.QueryHandlers;

public class DeleteOrganizationQueryHandler
    : DeleteCommandHandlerBase<
        DeleteOrganizationQuery,
        OrganizationDto,
        OrganizationId,
        Guid,
        OpenGitBaseDbContext,
        Entities.OrganizationEntity
    >
{
    public DeleteOrganizationQueryHandler(IDbContextFactory<OpenGitBaseDbContext> contextFactory)
        : base(contextFactory) { }
}
