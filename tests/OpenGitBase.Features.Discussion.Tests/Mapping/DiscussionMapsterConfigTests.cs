using MapsterMapper;
using OpenGitBase.Common.Tests.Mapping;
using OpenGitBase.Features.Discussion;
using OpenGitBase.Features.Discussion.Contracts;
using OpenGitBase.Features.Discussion.Entities;

namespace OpenGitBase.Features.Discussion.Tests.Mapping;

public class DiscussionMapsterConfigTests
{
    private static readonly IMapper Mapper =
        MapsterTestMapperFactory.Create<DiscussionMapsterConfig>();

    [Fact]
    public void DiscussionEntity_MapsToDto()
    {
        var entity = new DiscussionEntity
        {
            Id = Guid.NewGuid(),
            RepositoryId = Guid.NewGuid(),
            Number = 1,
            Title = "Title",
            Status = (int)DiscussionStatus.Open,
            CreatorUserId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
        };

        var dto = Mapper.Map<DiscussionDto>(entity);
        Assert.Equal(entity.Title, dto.Title);
        Assert.Equal(DiscussionStatus.Open, dto.Status);
    }
}
