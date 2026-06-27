using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.MergeRequest.Contracts;

public class CloseMergeRequestQuery : IQuery<MergeRequestDto, CloseMergeRequestQuery>
{
    public Guid RepositoryId { get; set; }
    public int Number { get; set; }
}
