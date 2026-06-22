using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Discussion.Contracts;

public class ListDiscussionCommentsQuery : IQuery<IReadOnlyList<DiscussionCommentDto>, ListDiscussionCommentsQuery>
{
    public Guid RepositoryId { get; set; }
    public int DiscussionNumber { get; set; }
}
