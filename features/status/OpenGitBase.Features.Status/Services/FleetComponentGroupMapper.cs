using OpenGitBase.Features.Status.Contracts;

namespace OpenGitBase.Features.Status.Services;

public static class FleetComponentGroupMapper
{
    public static StatusComponentGroup ToGroup(FleetComponentType componentType) =>
        componentType switch
        {
            FleetComponentType.Website => StatusComponentGroup.Website,
            FleetComponentType.Api => StatusComponentGroup.Api,
            FleetComponentType.Git => StatusComponentGroup.Git,
            _ => StatusComponentGroup.Website,
        };
}
