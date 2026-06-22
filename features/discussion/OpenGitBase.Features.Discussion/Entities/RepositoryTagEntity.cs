#pragma warning disable SA1402 // File may only contain a single type
using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Discussion.Entities;

public class RepositoryTagEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }
    public Guid RepositoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<DiscussionTagAssignmentEntity> Assignments { get; set; } = [];
}

public class DiscussionTagAssignmentEntity
{
    public Guid DiscussionId { get; set; }
    public Guid TagId { get; set; }

    public DiscussionEntity? Discussion { get; set; }
    public RepositoryTagEntity? Tag { get; set; }
}
