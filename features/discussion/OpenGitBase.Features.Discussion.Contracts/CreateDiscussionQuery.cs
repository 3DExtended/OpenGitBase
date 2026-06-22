using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Discussion.Contracts;

public class CreateDiscussionQuery : IQuery<DiscussionDto, CreateDiscussionQuery>
{
    public Guid RepositoryId { get; set; }
    public UserId CreatorUserId { get; set; } = UserId.From(Guid.Empty);
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public UserId? AssigneeUserId { get; set; }
    public IReadOnlyList<Guid> TagIds { get; set; } = [];
}
