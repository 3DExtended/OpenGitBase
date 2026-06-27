using OpenGitBase.Cqrs;

namespace OpenGitBase.Features.MergeRequest.Contracts;

public class PublishMergeRequestQuery : IQuery<MergeRequestDto, PublishMergeRequestQuery>
{
    public Guid RepositoryId { get; set; }
    public int Number { get; set; }
}
