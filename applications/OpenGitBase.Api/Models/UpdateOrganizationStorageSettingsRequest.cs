using OpenGitBase.Features.Organization.Contracts;

namespace OpenGitBase.Api.Models;

public sealed class UpdateOrganizationStorageSettingsRequest
{
    public PlacementPolicy DefaultPlacementPolicy { get; init; }

    public SelfHostPreference DefaultSelfHostPreference { get; init; }
}
