using MapsterMapper;
using OpenGitBase.Common.Tests.Mapping;
using OpenGitBase.Features.MergeRequest;
using OpenGitBase.Features.MergeRequest.Contracts;
using OpenGitBase.Features.MergeRequest.Entities;

namespace OpenGitBase.Features.MergeRequest.Tests.Mapping;

public class MergeRequestMapsterConfigTests
{
    private static readonly IMapper Mapper =
        MapsterTestMapperFactory.Create<MergeRequestMapsterConfig>();

    private static readonly (Type Type, string Name)[] ExcludedProperties =
    [
        (typeof(MergeRequestDto), nameof(MergeRequestDto.CreatorUsername)),
        (typeof(MergeRequestDto), nameof(MergeRequestDto.Approvals)),
        (typeof(MergeRequestDto), nameof(MergeRequestDto.RequiredApprovalCount)),
        (typeof(MergeRequestDto), nameof(MergeRequestDto.ApprovalCountAtHead)),
        (typeof(MergeRequestDto), nameof(MergeRequestDto.MergeCommitSha)),
    ];

    [Fact]
    public void MergeRequestEntity_MergeRequestDto_RoundTripsAllProperties() =>
        MapsterMappingAssert.AssertRoundTrip<MergeRequestEntity, MergeRequestDto>(
            Mapper,
            ExcludedProperties
        );

    [Fact]
    public void MergeRequestDto_MergeRequestEntity_RoundTripsAllProperties() =>
        MapsterMappingAssert.AssertRoundTrip<MergeRequestDto, MergeRequestEntity>(
            Mapper,
            ExcludedProperties
        );
}
