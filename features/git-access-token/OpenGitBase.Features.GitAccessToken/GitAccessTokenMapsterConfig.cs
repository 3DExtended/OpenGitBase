using Mapster;
using OpenGitBase.Features.GitAccessToken.Contracts;
using OpenGitBase.Features.GitAccessToken.Entities;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.GitAccessToken;

public class GitAccessTokenMapsterConfig : IRegister
{
    public void Register(TypeAdapterConfig config)
    {
        config.NewConfig<Entities.GitAccessTokenEntity, GitAccessTokenDto>()
            .Map(dest => dest.Id, src => GitAccessTokenId.From(src.Id))
            .Map(dest => dest.OwnerUserId, src => UserId.From(src.OwnerUserId))
            .Map(dest => dest.Name, src => src.Name)
            .Map(dest => dest.Scope, src => src.Scope)
            .Map(dest => dest.CreatedAt, src => src.CreatedAt)
            .Map(dest => dest.ExpiresAt, src => src.ExpiresAt)
            .Map(dest => dest.RevokedAt, src => src.RevokedAt);
    }
}
