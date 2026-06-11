using MapsterMapper;
using OpenGitBase.Common.Tests.Mapping;
using OpenGitBase.Features.PublicGitSshKey;
using OpenGitBase.Features.PublicGitSshKey.Contracts;
using OpenGitBase.Features.PublicGitSshKey.Entities;

namespace OpenGitBase.Features.PublicGitSshKey.Tests.Mapping;

public class PublicGitSshKeyMapsterConfigTests
{
    private static readonly IMapper Mapper =
        MapsterTestMapperFactory.Create<PublicGitSshKeyMapsterConfig>();

    private static readonly (Type Type, string Name)[] ExcludedProperties =
    [
        (typeof(PublicGitSshKeyEntity), nameof(PublicGitSshKeyEntity.OwnerUser)),
    ];

    [Fact]
    public void PublicGitSshKeyEntity_PublicGitSshKeyDto_RoundTripsAllProperties() =>
        MapsterMappingAssert.AssertRoundTrip<PublicGitSshKeyEntity, PublicGitSshKeyDto>(
            Mapper,
            ExcludedProperties
        );

    [Fact]
    public void PublicGitSshKeyDto_PublicGitSshKeyEntity_RoundTripsAllProperties() =>
        MapsterMappingAssert.AssertRoundTrip<PublicGitSshKeyDto, PublicGitSshKeyEntity>(
            Mapper,
            ExcludedProperties
        );
}
