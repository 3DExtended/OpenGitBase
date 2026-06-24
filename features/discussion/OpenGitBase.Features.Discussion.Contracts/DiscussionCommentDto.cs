using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Discussion.Contracts;

public class DiscussionCommentDto : ModelBase<DiscussionCommentId, Guid>
{
    public DiscussionId DiscussionId { get; set; } = DiscussionId.From(Guid.Empty);
    public UserId AuthorUserId { get; set; } = UserId.From(Guid.Empty);
    public string BodyMarkdown { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? EditedAt { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public UserId? DeletedByUserId { get; set; }
    public bool IsDeleted { get; set; }
    public DiscussionCommentId? ParentCommentId { get; set; }
    public bool IsResolved { get; set; }
    public DateTimeOffset? ResolvedAt { get; set; }
    public UserId? ResolvedByUserId { get; set; }
    public int ReplyCount { get; set; }
    public DateTimeOffset? LastReplyAt { get; set; }
    public bool OrphanedFromDeletedRoot { get; set; }
    public IReadOnlyList<DiscussionCommentDto> Replies { get; set; } = [];
    public CommentAnchorDto? Anchor { get; set; }
}
