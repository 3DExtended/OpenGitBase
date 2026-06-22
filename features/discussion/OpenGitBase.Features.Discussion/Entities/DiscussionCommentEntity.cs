using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Discussion.Entities;

public class DiscussionCommentEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }
    public Guid DiscussionId { get; set; }
    public Guid AuthorUserId { get; set; }
    public string BodyMarkdown { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? EditedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public Guid? DeletedByUserId { get; set; }

    public DiscussionEntity? Discussion { get; set; }
    public CommentAnchorEntity? Anchor { get; set; }
}
