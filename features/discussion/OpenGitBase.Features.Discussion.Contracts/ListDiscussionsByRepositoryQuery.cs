using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Discussion.Contracts;

public class ListDiscussionsByRepositoryQuery : IQuery<IReadOnlyList<DiscussionDto>, ListDiscussionsByRepositoryQuery>
{
    public Guid RepositoryId { get; set; }
    public DiscussionStatus? Status { get; set; }
    public Guid? AssigneeUserId { get; set; }
    public Guid? TagId { get; set; }
}
