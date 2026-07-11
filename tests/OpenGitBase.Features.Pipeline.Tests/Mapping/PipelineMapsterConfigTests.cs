using MapsterMapper;
using OpenGitBase.Common.Tests.Mapping;
using OpenGitBase.Features.Pipeline;
using OpenGitBase.Features.Pipeline.Contracts;
using OpenGitBase.Features.Pipeline.Entities;

namespace OpenGitBase.Features.Pipeline.Tests.Mapping;

public class PipelineMapsterConfigTests
{
    private static readonly IMapper Mapper =
        MapsterTestMapperFactory.Create<PipelineMapsterConfig>();

    private static readonly (Type Type, string Name)[] ExcludedProperties = [];

    [Fact]
    public void PipelineEntity_PipelineDto_RoundTripsAllProperties() =>
        MapsterMappingAssert.AssertRoundTrip<PipelineEntity, PipelineDto>(
            Mapper,
            ExcludedProperties
        );

    [Fact]
    public void PipelineDto_PipelineEntity_RoundTripsAllProperties() =>
        MapsterMappingAssert.AssertRoundTrip<PipelineDto, PipelineEntity>(
            Mapper,
            ExcludedProperties
        );
}
