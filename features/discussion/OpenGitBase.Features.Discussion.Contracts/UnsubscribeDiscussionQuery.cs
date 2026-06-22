using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Discussion.Contracts;

public class UnsubscribeDiscussionQuery : IQuery<Unit, UnsubscribeDiscussionQuery>
{
    public DiscussionId DiscussionId { get; set; } = DiscussionId.From(Guid.Empty);
    public UserId UserId { get; set; } = UserId.From(Guid.Empty);
}
