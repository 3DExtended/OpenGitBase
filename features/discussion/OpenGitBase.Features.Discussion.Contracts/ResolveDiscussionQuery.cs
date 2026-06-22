using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Discussion.Contracts;

public class ResolveDiscussionQuery : IQuery<DiscussionDto, ResolveDiscussionQuery>
{
    public Guid RepositoryId { get; set; }
    public int Number { get; set; }
}
