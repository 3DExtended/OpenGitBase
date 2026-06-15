using Mapster;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.RepositoryMember.Entities;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.RepositoryMember;

public class RepositoryMemberMapsterConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config
            .NewConfig<RepositoryMemberEntity, RepositoryMemberDto>()
            .Map(dest => dest.Id, src => RepositoryMemberId.From(src.Id))
            .Map(dest => dest.RepositoryId, src => RepositoryId.From(src.RepositoryId))
            .Map(dest => dest.UserId, src => UserId.From(src.UserId))
            .Map(dest => dest.Role, src => src.Role)
            .Ignore(dest => dest.Username);

        config
            .NewConfig<RepositoryMemberDto, RepositoryMemberEntity>()
            .Map(dest => dest.Id, src => src.Id.Value == Guid.Empty ? Guid.NewGuid() : src.Id.Value)
            .Map(dest => dest.RepositoryId, src => src.RepositoryId.Value)
            .Map(dest => dest.UserId, src => src.UserId.Value)
            .Map(dest => dest.Role, src => src.Role);
    }
}
