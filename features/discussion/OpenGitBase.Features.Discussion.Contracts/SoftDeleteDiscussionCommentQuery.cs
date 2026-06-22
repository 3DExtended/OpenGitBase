using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Discussion.Contracts;

public class SoftDeleteDiscussionCommentQuery : IQuery<DiscussionCommentDto, SoftDeleteDiscussionCommentQuery>
{
    public Guid CommentId { get; set; }
    public UserId ActingUserId { get; set; } = UserId.From(Guid.Empty);
    public bool IsModerator { get; set; }
}
