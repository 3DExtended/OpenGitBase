using Mapster;
using OpenGitBase.Features.PublicGitSshKey.Contracts;
using OpenGitBase.Features.PublicGitSshKey.Entities;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.PublicGitSshKey;

public class PublicGitSshKeyMapsterConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config
            .NewConfig<Entities.PublicGitSshKeyEntity, PublicGitSshKeyDto>()
            .Map(dest => dest.Id, src => PublicGitSshKeyId.From(src.Id))
            .Map(dest => dest.OwnerUserId, src => UserId.From(src.OwnerUserId))
            .Map(dest => dest.PublicSSHKey, src => src.PublicSSHKey)
            .Map(dest => dest.Name, src => src.Name)
            .Map(dest => dest.Fingerprint, src => src.Fingerprint);

        config
            .NewConfig<PublicGitSshKeyDto, Entities.PublicGitSshKeyEntity>()
            .Map(dest => dest.Id, src => src.Id.Value == Guid.Empty ? Guid.NewGuid() : src.Id.Value)
            .Map(dest => dest.OwnerUserId, src => src.OwnerUserId.Value)
            .Map(dest => dest.PublicSSHKey, src => src.PublicSSHKey)
            .Map(dest => dest.Fingerprint, src => src.Fingerprint)
            .Map(dest => dest.Name, src => src.Name);
    }
}
