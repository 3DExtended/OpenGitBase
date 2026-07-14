namespace OpenGitBase.Features.Discussion.Entities;

public class DiscussionLinkEntity
{
    public Guid SourceDiscussionId { get; set; }

    public Guid TargetDiscussionId { get; set; }

    public int RelationshipType { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DiscussionEntity SourceDiscussion { get; set; } = null!;

    public DiscussionEntity TargetDiscussion { get; set; } = null!;
}
