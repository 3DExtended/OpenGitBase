using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.Discussion.Contracts;

public class DeleteRepositoryTagQuery : IQuery<Unit, DeleteRepositoryTagQuery>
{
    public Guid TagId { get; set; }
    public Guid RepositoryId { get; set; }
}
