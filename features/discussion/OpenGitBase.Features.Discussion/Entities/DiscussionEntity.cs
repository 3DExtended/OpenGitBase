using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Discussion.Entities;

public class DiscussionEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public int Status { get; set; }
    public bool HasEverBeenEngaged { get; set; }
    public Guid CreatorUserId { get; set; }
    public Guid? AssigneeUserId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public ICollection<DiscussionCommentEntity> Comments { get; set; } = [];
    public ICollection<DiscussionTagAssignmentEntity> TagAssignments { get; set; } = [];
    public ICollection<DiscussionSubscriptionEntity> Subscriptions { get; set; } = [];
}
