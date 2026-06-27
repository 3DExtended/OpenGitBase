using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.MergeRequest.Contracts;

public class GetMergeRequestByNumberQuery : IQuery<MergeRequestDto, GetMergeRequestByNumberQuery>
{
    public Guid RepositoryId { get; set; }
    public int Number { get; set; }
}
