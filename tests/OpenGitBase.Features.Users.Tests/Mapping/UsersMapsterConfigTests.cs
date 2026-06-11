using MapsterMapper;
using OpenGitBase.Common.Tests.Mapping;
using OpenGitBase.Features.Users;
using OpenGitBase.Features.Users.Contracts.Models;
using OpenGitBase.Features.Users.Entities;

namespace OpenGitBase.Features.Users.Tests.Mapping;

public class UsersMapsterConfigTests
{
    private static readonly IMapper Mapper = MapsterTestMapperFactory.Create<UsersMapsterConfig>();

    private static readonly (Type Type, string Name)[] ExcludedProperties =
    [
        (typeof(UserEntity), nameof(UserEntity.UserCredentials)),
        (typeof(UserEntity), nameof(UserEntity.NormalizedUsername)),
        (typeof(UserEntity), nameof(UserEntity.CreatedAt)),
        (typeof(UserCredentialsEntity), nameof(UserCredentialsEntity.User)),
    ];

    [Fact]
    public void User_UserEntity_RoundTripsAllProperties() =>
        MapsterMappingAssert.AssertRoundTrip<User, UserEntity>(Mapper, ExcludedProperties);

    [Fact]
    public void UserEntity_User_RoundTripsAllProperties() =>
        MapsterMappingAssert.AssertRoundTrip<UserEntity, User>(Mapper, ExcludedProperties);

    [Fact]
    public void UserCredentials_UserCredentialsEntity_RoundTripsAllProperties() =>
        MapsterMappingAssert.AssertRoundTrip<UserCredentials, UserCredentialsEntity>(
            Mapper,
            ExcludedProperties
        );

    [Fact]
    public void UserCredentialsEntity_UserCredentials_RoundTripsAllProperties() =>
        MapsterMappingAssert.AssertRoundTrip<UserCredentialsEntity, UserCredentials>(
            Mapper,
            ExcludedProperties
        );
}
