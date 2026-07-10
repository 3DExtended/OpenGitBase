namespace OpenGitBase.Features.Organization.Contracts;

public sealed class OrganizationStorageSettingsDto
{
    public Guid OrganizationId { get; set; }

    public PlacementPolicy DefaultPlacementPolicy { get; set; } = PlacementPolicy.Inherit;

    public SelfHostPreference DefaultSelfHostPreference { get; set; } =
        SelfHostPreference.PlatformOnly;

    public long BytesLimit { get; set; }

    public long PlatformBytesLimit { get; set; }

    public long ContributedBytesCapacity { get; set; }
}
