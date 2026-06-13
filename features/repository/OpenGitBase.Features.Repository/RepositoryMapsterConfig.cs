using Mapster;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Repository;

public class RepositoryMapsterConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config
            .NewConfig<Entities.RepositoryEntity, RepositoryDto>()
            .Map(dest => dest.Id, src => RepositoryId.From(src.Id))
            .Map(dest => dest.OwnerUserId, src => UserId.From(src.OwnerUserId))
            .Map(dest => dest.Name, src => src.Name)
            .Map(dest => dest.Slug, src => src.Slug)
            .Map(dest => dest.PhysicalPath, src => src.PhysicalPath)
            .Map(dest => dest.IsPrivate, src => src.IsPrivate);

        config
            .NewConfig<RepositoryDto, Entities.RepositoryEntity>()
            .Map(dest => dest.Id, src => src.Id.Value == Guid.Empty ? Guid.NewGuid() : src.Id.Value)
            .Map(dest => dest.OwnerUserId, src => src.OwnerUserId.Value)
            .Map(dest => dest.Name, src => src.Name)
            .Map(dest => dest.Slug, src => src.Slug)
            .Map(dest => dest.PhysicalPath, src => src.PhysicalPath)
            .Map(dest => dest.IsPrivate, src => src.IsPrivate);
    }
}
