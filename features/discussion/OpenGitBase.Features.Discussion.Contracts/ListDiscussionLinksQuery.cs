using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Discussion.Contracts;

public class ListDiscussionLinksQuery
    : IQuery<IReadOnlyList<DiscussionLinkDto>, ListDiscussionLinksQuery>
{
    public Guid RepositoryId { get; set; }

    public int Number { get; set; }
}
