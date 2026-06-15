using MapsterMapper;
using OpenGitBase.Common.Tests.Mapping;
using OpenGitBase.Features.StorageNode;
using OpenGitBase.Features.StorageNode.Contracts;
using OpenGitBase.Features.StorageNode.Entities;

namespace OpenGitBase.Features.StorageNode.Tests.Mapping;

public class StorageNodeMapsterConfigTests
{
    private static readonly IMapper Mapper =
        MapsterTestMapperFactory.Create<StorageNodeMapsterConfig>();

    private static readonly (Type Type, string Name)[] ExcludedProperties =
    [
        (typeof(StorageNodeEntity), nameof(StorageNodeEntity.ApiTokenHash)),
        (typeof(StorageNodeEntity), nameof(StorageNodeEntity.ApiTokenProtected)),
    ];

    [Fact]
    public void StorageNodeEntity_StorageNodeDto_RoundTripsAllProperties() =>
        MapsterMappingAssert.AssertRoundTrip<StorageNodeEntity, StorageNodeDto>(
            Mapper,
            ExcludedProperties
        );

    [Fact]
    public void StorageNodeDto_StorageNodeEntity_RoundTripsAllProperties() =>
        MapsterMappingAssert.AssertRoundTrip<StorageNodeDto, StorageNodeEntity>(
            Mapper,
            ExcludedProperties
        );
}
