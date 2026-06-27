using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.MergeRequest.Contracts;

public class ListMergeRequestsByRepositoryQuery
    : IQuery<IReadOnlyList<MergeRequestDto>, ListMergeRequestsByRepositoryQuery>
{
    public Guid RepositoryId { get; set; }
    public MergeRequestStatus? Status { get; set; }
}
