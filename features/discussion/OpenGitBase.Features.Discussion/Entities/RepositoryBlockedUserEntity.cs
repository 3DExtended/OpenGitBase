#pragma warning disable SA1402 // File may only contain a single type
using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Discussion.Entities;

public class RepositoryBlockedUserEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public Guid UserId { get; set; }
    public Guid BlockedByUserId { get; set; }
    public DateTimeOffset BlockedAt { get; set; }
    public string? Reason { get; set; }
}

public class DiscussionSubscriptionEntity
{
    public Guid DiscussionId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset SubscribedAt { get; set; }
    public bool IsActive { get; set; } = true;

    public DiscussionEntity? Discussion { get; set; }
}

public class UserNotificationEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid DiscussionId { get; set; }
    public int EventType { get; set; }
    public string Message { get; set; } = string.Empty;
    public Guid? ActorUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? ReadAt { get; set; }

    public DiscussionEntity? Discussion { get; set; }
}
