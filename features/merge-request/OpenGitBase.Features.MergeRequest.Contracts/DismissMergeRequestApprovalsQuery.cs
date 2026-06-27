using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.MergeRequest.Contracts;

public class DismissMergeRequestApprovalsQuery
    : IQuery<MergeRequestDto, DismissMergeRequestApprovalsQuery>
{
    public Guid RepositoryId { get; set; }

    public int Number { get; set; }
}
