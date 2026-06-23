using OpenGitBase.Cqrs.EfCore;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Discussion.Contracts;

public class NotificationDto : ModelBase<NotificationId, Guid>
{
    public UserId UserId { get; set; } = UserId.From(Guid.Empty);
    public DiscussionId DiscussionId { get; set; } = DiscussionId.From(Guid.Empty);
    public Guid RepositoryId { get; set; }
    public int DiscussionNumber { get; set; }
    public DiscussionCommentId? CommentId { get; set; }
    public string OwnerSlug { get; set; } = string.Empty;
    public string RepositorySlug { get; set; } = string.Empty;
    public NotificationEventType EventType { get; set; }
    public string Message { get; set; } = string.Empty;
    public UserId? ActorUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }
    public bool IsRead => ReadAt is not null;
}
