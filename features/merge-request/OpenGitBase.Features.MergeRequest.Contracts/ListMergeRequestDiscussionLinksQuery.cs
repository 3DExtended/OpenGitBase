using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.MergeRequest.Contracts;

public class ListMergeRequestDiscussionLinksQuery
    : IQuery<IReadOnlyList<MergeRequestDiscussionLinkDto>, ListMergeRequestDiscussionLinksQuery>
{
    public Guid RepositoryId { get; set; }

    public int Number { get; set; }
}
