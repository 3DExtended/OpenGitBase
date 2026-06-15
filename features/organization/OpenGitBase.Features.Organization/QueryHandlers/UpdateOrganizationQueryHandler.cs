using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.Entities;

namespace OpenGitBase.Features.Organization.QueryHandlers;

public class UpdateOrganizationQueryHandler
    : UpdateCommandHandlerBase<
        UpdateOrganizationQuery,
        OrganizationDto,
        OrganizationId,
        Guid,
        OpenGitBaseDbContext,
        Entities.OrganizationEntity
    >
{
    public UpdateOrganizationQueryHandler(
        IMapper mapper,
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
        : base(mapper, contextFactory) { }
}
