using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Discussion.Contracts;

public class SubscribeDiscussionQuery : IQuery<Unit, SubscribeDiscussionQuery>
{
    public DiscussionId DiscussionId { get; set; } = DiscussionId.From(Guid.Empty);
    public UserId UserId { get; set; } = UserId.From(Guid.Empty);
}
