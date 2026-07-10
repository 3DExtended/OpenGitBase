using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.Entities;

namespace OpenGitBase.Features.Organization.QueryHandlers;

public sealed class GetOrganizationStorageSettingsQueryHandler
    : IQueryHandler<GetOrganizationStorageSettingsQuery, OrganizationStorageSettingsDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public GetOrganizationStorageSettingsQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<OrganizationStorageSettingsDto>> RunQueryAsync(
        GetOrganizationStorageSettingsQuery query,
        CancellationToken cancellationToken
    )
    {
        await using var context = await _contextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);

        var organizationExists = await context
            .Set<OrganizationEntity>()
            .AsNoTracking()
            .AnyAsync(
                organization => organization.Id == query.OrganizationId.Value,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (!organizationExists)
        {
            return Option<OrganizationStorageSettingsDto>.None;
        }

        var settings = await context
            .Set<OrganizationStorageSettingsEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                entity => entity.OrganizationId == query.OrganizationId.Value,
                cancellationToken
            )
            .ConfigureAwait(false);

        return Option.From(
            new OrganizationStorageSettingsDto
            {
                OrganizationId = query.OrganizationId.Value,
                DefaultPlacementPolicy =
                    settings?.DefaultPlacementPolicy ?? PlacementPolicy.Inherit,
                DefaultSelfHostPreference =
                    settings?.DefaultSelfHostPreference ?? SelfHostPreference.PlatformOnly,
            }
        );
    }
}
