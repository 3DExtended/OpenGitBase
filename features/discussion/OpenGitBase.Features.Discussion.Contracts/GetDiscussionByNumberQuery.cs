using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Discussion.Contracts;

public class GetDiscussionByNumberQuery : IQuery<DiscussionDto, GetDiscussionByNumberQuery>
{
    public Guid RepositoryId { get; set; }
    public int Number { get; set; }
    public bool IncludeComments { get; set; }
}
