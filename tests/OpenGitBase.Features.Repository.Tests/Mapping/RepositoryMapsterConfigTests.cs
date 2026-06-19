using MapsterMapper;
using OpenGitBase.Common.Tests.Mapping;
using OpenGitBase.Features.Repository;
using OpenGitBase.Features.Repository.Contracts;
using OpenGitBase.Features.Repository.Entities;

namespace OpenGitBase.Features.Repository.Tests.Mapping;

public class RepositoryMapsterConfigTests
{
    private static readonly IMapper Mapper =
        MapsterTestMapperFactory.Create<RepositoryMapsterConfig>();

    private static readonly (Type Type, string Name)[] ExcludedProperties =
    [
        (typeof(RepositoryDto), nameof(RepositoryDto.OwnerKind)),
        (typeof(RepositoryDto), nameof(RepositoryDto.OwnerSlug)),
    ];

    [Fact]
    public void RepositoryEntity_RepositoryDto_RoundTripsAllProperties() =>
        MapsterMappingAssert.AssertRoundTrip<RepositoryEntity, RepositoryDto>(
            Mapper,
            ExcludedProperties
        );

    [Fact]
    public void RepositoryDto_RepositoryEntity_RoundTripsAllProperties() =>
        MapsterMappingAssert.AssertRoundTrip<RepositoryDto, RepositoryEntity>(
            Mapper,
            ExcludedProperties
        );
}
