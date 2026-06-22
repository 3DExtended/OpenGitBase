using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Discussion.Contracts;

public class CreateDiscussionNotificationQuery : IQuery<Unit, CreateDiscussionNotificationQuery>
{
    public DiscussionId DiscussionId { get; set; } = DiscussionId.From(Guid.Empty);
    public NotificationEventType EventType { get; set; }
    public UserId ActorUserId { get; set; } = UserId.From(Guid.Empty);
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<UserId> AdditionalRecipientUserIds { get; set; } = [];
}
