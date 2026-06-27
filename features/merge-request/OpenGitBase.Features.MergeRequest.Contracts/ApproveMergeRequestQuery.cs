using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.MergeRequest.Contracts;

public class ApproveMergeRequestQuery : IQuery<MergeRequestDto, ApproveMergeRequestQuery>
{
    public Guid RepositoryId { get; set; }

    public int Number { get; set; }

    public UserId ApproverUserId { get; set; } = UserId.From(Guid.Empty);
}
