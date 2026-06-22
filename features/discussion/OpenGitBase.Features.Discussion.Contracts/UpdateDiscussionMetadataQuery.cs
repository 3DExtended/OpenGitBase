using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Discussion.Contracts;

public class UpdateDiscussionMetadataQuery : IQuery<DiscussionDto, UpdateDiscussionMetadataQuery>
{
    public Guid RepositoryId { get; set; }
    public int Number { get; set; }
    public UserId ActingUserId { get; set; } = UserId.From(Guid.Empty);
    public string? Title { get; set; }
    public UserId? AssigneeUserId { get; set; }
    public bool ClearAssignee { get; set; }
    public IReadOnlyList<Guid>? TagIds { get; set; }
}
