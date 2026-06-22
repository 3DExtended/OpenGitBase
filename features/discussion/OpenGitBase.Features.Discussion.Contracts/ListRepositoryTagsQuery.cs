using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Discussion.Contracts;

public class ListRepositoryTagsQuery : IQuery<IReadOnlyList<RepositoryTagDto>, ListRepositoryTagsQuery>
{
    public Guid RepositoryId { get; set; }
}
