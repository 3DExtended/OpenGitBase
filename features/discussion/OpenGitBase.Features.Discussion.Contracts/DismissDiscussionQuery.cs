using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Discussion.Contracts;

public class DismissDiscussionQuery : IQuery<DiscussionDto, DismissDiscussionQuery>
{
    public Guid RepositoryId { get; set; }
    public int Number { get; set; }
}
