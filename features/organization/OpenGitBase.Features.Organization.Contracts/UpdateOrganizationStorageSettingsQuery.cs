using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Organization.Contracts;

public sealed class UpdateOrganizationStorageSettingsQuery
    : IQuery<OrganizationStorageSettingsDto, UpdateOrganizationStorageSettingsQuery>
{
    public OrganizationId OrganizationId { get; set; } = default!;

    public PlacementPolicy DefaultPlacementPolicy { get; set; }

    public SelfHostPreference DefaultSelfHostPreference { get; set; }
}
