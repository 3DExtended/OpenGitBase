using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.Entities;

namespace OpenGitBase.Features.Organization.QueryHandlers;

public class ListUserOwnedOrganizationsQueryHandler
    : IQueryHandler<ListUserOwnedOrganizationsQuery, IReadOnlyList<OrganizationSummaryDto>>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public ListUserOwnedOrganizationsQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<IReadOnlyList<OrganizationSummaryDto>>> RunQueryAsync(
        ListUserOwnedOrganizationsQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory.CreateDbContextAsync(cancellationToken);
        var orgs = await context
            .Set<OrganizationEntity>()
            .AsNoTracking()
            .Where(x => x.OwnerUserId == query.UserId.Value)
            .Select(x => new OrganizationSummaryDto { Name = x.Name, Slug = x.Slug })
            .ToListAsync(cancellationToken);

        return Option.From<IReadOnlyList<OrganizationSummaryDto>>(orgs);
    }
}
