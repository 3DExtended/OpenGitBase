using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Common.Services;
using OpenGitBase.Cqrs;
using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.Entities;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Organization.QueryHandlers;

public class CreateOrganizationQueryHandler
    : CreateQueryHandlerBase<
        CreateOrganizationQuery,
        OrganizationDto,
        OrganizationId,
        Guid,
        OpenGitBaseDbContext,
        OrganizationEntity
    >
{
    private readonly IQueryProcessor _queryProcessor;

    public CreateOrganizationQueryHandler(
        IMapper mapper,
        IDbContextFactory<OpenGitBaseDbContext> contextFactory,
        IQueryProcessor queryProcessor
    )
        : base(mapper, contextFactory)
    {
        _queryProcessor = queryProcessor;
    }

    protected override Task<Option<OrganizationDto>> PrepareModelAsync(
        OrganizationDto model,
        OpenGitBaseDbContext context,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(model.Slug) || ReservedSlugValidator.IsReserved(model.Slug))
        {
            return Task.FromResult(Option<OrganizationDto>.None);
        }

        return Task.FromResult(Option.From(model));
    }

    protected override async Task AfterCreationAsync(
        CreateOrganizationQuery query,
        OrganizationId id,
        CancellationToken cancellationToken
    )
    {
        if (query.CreatorUserId == Guid.Empty)
        {
            return;
        }

        await _queryProcessor
            .RunQueryAsync(
                new AddOrganizationMemberQuery
                {
                    OrganizationId = id,
                    UserId = UserId.From(query.CreatorUserId),
                    Role = OrganizationMemberRole.Owner,
                },
                cancellationToken
            )
            .ConfigureAwait(false);
    }
}
