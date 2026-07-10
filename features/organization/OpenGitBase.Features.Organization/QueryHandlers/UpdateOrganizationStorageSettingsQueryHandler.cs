using Microsoft.EntityFrameworkCore;
using OpenGitBase.Common.Data;
using OpenGitBase.Cqrs;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.Entities;

namespace OpenGitBase.Features.Organization.QueryHandlers;

public sealed class UpdateOrganizationStorageSettingsQueryHandler
    : IQueryHandler<UpdateOrganizationStorageSettingsQuery, OrganizationStorageSettingsDto>
{
    private readonly IDbContextFactory<OpenGitBaseDbContext> _contextFactory;

    public UpdateOrganizationStorageSettingsQueryHandler(
        IDbContextFactory<OpenGitBaseDbContext> contextFactory
    )
    {
        _contextFactory = contextFactory;
    }

    public async Task<Option<OrganizationStorageSettingsDto>> RunQueryAsync(
        UpdateOrganizationStorageSettingsQuery query,
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

        var entity = await context
            .Set<OrganizationStorageSettingsEntity>()
            .FirstOrDefaultAsync(
                settings => settings.OrganizationId == query.OrganizationId.Value,
                cancellationToken
            )
            .ConfigureAwait(false);

        if (entity is null)
        {
            entity = new OrganizationStorageSettingsEntity
            {
                Id = Guid.NewGuid(),
                OrganizationId = query.OrganizationId.Value,
            };
            context.Set<OrganizationStorageSettingsEntity>().Add(entity);
        }

        entity.DefaultPlacementPolicy = query.DefaultPlacementPolicy;
        entity.DefaultSelfHostPreference = query.DefaultSelfHostPreference;
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Option.From(
            new OrganizationStorageSettingsDto
            {
                OrganizationId = query.OrganizationId.Value,
                DefaultPlacementPolicy = entity.DefaultPlacementPolicy,
                DefaultSelfHostPreference = entity.DefaultSelfHostPreference,
            }
        );
    }
}
