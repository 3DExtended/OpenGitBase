using Mapster;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.Entities;

namespace OpenGitBase.Features.Organization;

public class OrganizationMapsterConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config
            .NewConfig<OrganizationEntity, OrganizationDto>()
            .Map(dest => dest.Id, src => OrganizationId.From(src.Id))
            .Map(dest => dest.Name, src => src.Name)
            .Map(dest => dest.Slug, src => src.Slug)
            .Map(dest => dest.OwnerUserId, src => src.OwnerUserId);

        config
            .NewConfig<OrganizationDto, OrganizationEntity>()
            .Map(dest => dest.Id, src => src.Id.Value == Guid.Empty ? Guid.NewGuid() : src.Id.Value)
            .Map(dest => dest.Name, src => src.Name)
            .Map(dest => dest.Slug, src => src.Slug)
            .Map(dest => dest.OwnerUserId, src => src.OwnerUserId);
    }
}
