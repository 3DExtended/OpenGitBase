using Mapster;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Users;

public class UsersMapsterConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config
            .NewConfig<User, UserEntity>()
            .Map(dest => dest.Id, src => src.Id.Value)
            .Map(dest => dest.Username, src => src.Username);

        config
            .NewConfig<UserEntity, User>()
            .Map(dest => dest.Id, src => UserId.From(src.Id))
            .Map(dest => dest.Username, src => src.Username);

        config
            .NewConfig<UserCredentials, UserCredentialsEntity>()
            .Map(dest => dest.UserId, src => src.Id.Value);

        config
            .NewConfig<UserCredentialsEntity, UserCredentials>()
            .Map(dest => dest.Id, src => UserCredentialsId.From(src.UserId));
    }
}
