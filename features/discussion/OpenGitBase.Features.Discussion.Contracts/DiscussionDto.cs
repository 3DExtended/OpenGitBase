using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Discussion.Contracts;

public class DiscussionDto : ModelBase<DiscussionId, Guid>
{
    public Guid RepositoryId { get; set; }
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public DiscussionStatus Status { get; set; }
    public bool HasEverBeenEngaged { get; set; }
    public UserId CreatorUserId { get; set; } = UserId.From(Guid.Empty);
    public UserId? AssigneeUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public IReadOnlyList<RepositoryTagDto> Tags { get; set; } = [];
    public IReadOnlyList<DiscussionCommentDto>? Comments { get; set; }
}
