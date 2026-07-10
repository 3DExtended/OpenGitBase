using Mapster;
using OpenGitBase.Features.Status.Contracts;
using OpenGitBase.Features.Status.Entities;

namespace OpenGitBase.Features.Status;

public class StatusMapsterConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config
            .NewConfig<FleetComponentEntity, FleetComponentDto>()
            .Map(dest => dest.Id, src => FleetComponentId.From(src.Id));
    }
}
