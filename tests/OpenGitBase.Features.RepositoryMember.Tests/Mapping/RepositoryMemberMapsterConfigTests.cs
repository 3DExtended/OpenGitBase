using MapsterMapper;
using OpenGitBase.Common.Tests.Mapping;
using OpenGitBase.Features.RepositoryMember;
using OpenGitBase.Features.RepositoryMember.Contracts;
using OpenGitBase.Features.RepositoryMember.Entities;

namespace OpenGitBase.Features.RepositoryMember.Tests.Mapping;

public class RepositoryMemberMapsterConfigTests
{
    private static readonly IMapper Mapper =
        MapsterTestMapperFactory.Create<RepositoryMemberMapsterConfig>();

    private static readonly (Type Type, string Name)[] ExcludedProperties =
    [
        (typeof(RepositoryMemberEntity), nameof(RepositoryMemberEntity.Repository)),
        (typeof(RepositoryMemberEntity), nameof(RepositoryMemberEntity.User)),
    ];

    [Fact]
    public void RepositoryMemberEntity_RepositoryMemberDto_RoundTripsAllProperties() =>
        MapsterMappingAssert.AssertRoundTrip<RepositoryMemberEntity, RepositoryMemberDto>(
            Mapper,
            ExcludedProperties
        );

    [Fact]
    public void RepositoryMemberDto_RepositoryMemberEntity_RoundTripsAllProperties() =>
        MapsterMappingAssert.AssertRoundTrip<RepositoryMemberDto, RepositoryMemberEntity>(
            Mapper,
            ExcludedProperties
        );
}
