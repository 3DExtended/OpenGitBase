using MapsterMapper;
using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.Entities;

namespace OpenGitBase.Features.Organization.QueryHandlers;

public class ListUserOrganizationsQueryHandler
    : IQueryHandler<ListUserOrganizationsQuery, IReadOnlyList<OrganizationDto>>
{
    private readonly IMapper _mapper;
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public ListUserOrganizationsQueryHandler(
        IMapper mapper,
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _mapper = mapper;
        _contextFactory = contextFactory;
    }

    public async Task<Option<IReadOnlyList<OrganizationDto>>> RunQueryAsync(
        ListUserOrganizationsQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var orgs = await context
            .Set<OrganizationMemberEntity>()
            .AsNoTracking()
            .Where(x => x.UserId == query.UserId.Value)
            .Join(
                context.Set<OrganizationEntity>().AsNoTracking(),
                member => member.OrganizationId,
                org => org.Id,
                (_, org) => org
            )
            .ToListAsync(cancellationToken);

        return Option.From<IReadOnlyList<OrganizationDto>>(
            orgs.Select(org => _mapper.Map<OrganizationDto>(org)).ToList()
        );
    }
}
