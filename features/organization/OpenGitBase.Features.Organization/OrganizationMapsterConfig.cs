using Mapster;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.Entities;
using OpenGitBase.Features.Users.Contracts.Models;

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

        config
            .NewConfig<OrganizationMemberEntity, OrganizationMemberDto>()
            .Map(dest => dest.Id, src => OrganizationMemberId.From(src.Id))
            .Map(dest => dest.OrganizationId, src => OrganizationId.From(src.OrganizationId))
            .Map(dest => dest.UserId, src => UserId.From(src.UserId))
            .Map(dest => dest.Role, src => src.Role)
            .Ignore(dest => dest.Username);

        config
            .NewConfig<OrganizationMemberDto, OrganizationMemberEntity>()
            .Map(dest => dest.Id, src => src.Id.Value == Guid.Empty ? Guid.NewGuid() : src.Id.Value)
            .Map(dest => dest.OrganizationId, src => src.OrganizationId.Value)
            .Map(dest => dest.UserId, src => src.UserId.Value)
            .Map(dest => dest.Role, src => src.Role);
    }
}
