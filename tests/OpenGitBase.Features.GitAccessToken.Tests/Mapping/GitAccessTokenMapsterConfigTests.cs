using MapsterMapper;
using OpenGitBase.Common.Tests.Mapping;
using OpenGitBase.Features.GitAccessToken;
using OpenGitBase.Features.GitAccessToken.Contracts;
using OpenGitBase.Features.GitAccessToken.Entities;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.GitAccessToken.Tests.Mapping;

public class GitAccessTokenMapsterConfigTests
{
    private static readonly IMapper Mapper =
        MapsterTestMapperFactory.Create<GitAccessTokenMapsterConfig>();

    private static readonly (Type Type, string Name)[] ExcludedProperties =
    [
        (typeof(GitAccessTokenEntity), nameof(GitAccessTokenEntity.OwnerUser)),
        (typeof(GitAccessTokenEntity), nameof(GitAccessTokenEntity.TokenHash)),
        (typeof(GitAccessTokenEntity), nameof(GitAccessTokenEntity.TokenLookupHash)),
    ];

    [Fact]
    public void GitAccessTokenEntity_GitAccessTokenDto_MapsMetadata()
    {
        var entity = new GitAccessTokenEntity
        {
            Id = Guid.NewGuid(),
            OwnerUserId = Guid.NewGuid(),
            Name = "test",
            Scope = GitAccessTokenScopes.Read,
            CreatedAt = DateTimeOffset.UtcNow,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(1),
        };

        var dto = Mapper.Map<GitAccessTokenDto>(entity);

        Assert.Equal(entity.Name, dto.Name);
        Assert.Equal(entity.Scope, dto.Scope);
        Assert.Equal(UserId.From(entity.OwnerUserId), dto.OwnerUserId);
    }
}
