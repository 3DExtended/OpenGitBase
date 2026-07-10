using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Organization.Contracts;

namespace OpenGitBase.Features.Organization.Entities;

public class OrganizationStorageSettingsEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }

    public Guid OrganizationId { get; set; }

    public PlacementPolicy DefaultPlacementPolicy { get; set; } = PlacementPolicy.Inherit;

    public SelfHostPreference DefaultSelfHostPreference { get; set; } =
        SelfHostPreference.PlatformOnly;
}
