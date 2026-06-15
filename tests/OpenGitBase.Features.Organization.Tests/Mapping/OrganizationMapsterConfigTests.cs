using MapsterMapper;
using OpenGitBase.Common.Tests.Mapping;
using OpenGitBase.Features.Organization;
using OpenGitBase.Features.Organization.Contracts;
using OpenGitBase.Features.Organization.Entities;

namespace OpenGitBase.Features.Organization.Tests.Mapping;

public class OrganizationMapsterConfigTests
{
    private static readonly IMapper Mapper =
        MapsterTestMapperFactory.Create<OrganizationMapsterConfig>();

    private static readonly (Type Type, string Name)[] ExcludedProperties =
    [
        (typeof(OrganizationEntity), nameof(OrganizationEntity.OwnerUser)),
    ];

    [Fact]
    public void OrganizationEntity_OrganizationDto_RoundTripsAllProperties() =>
        MapsterMappingAssert.AssertRoundTrip<OrganizationEntity, OrganizationDto>(
            Mapper,
            ExcludedProperties
        );

    [Fact]
    public void OrganizationDto_OrganizationEntity_RoundTripsAllProperties() =>
        MapsterMappingAssert.AssertRoundTrip<OrganizationDto, OrganizationEntity>(
            Mapper,
            ExcludedProperties
        );
}
