using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Discussion.Contracts;

public class UnresolveSubThreadDiscussionCommentQuery
    : IQuery<DiscussionCommentDto, UnresolveSubThreadDiscussionCommentQuery>
{
    public DiscussionCommentId CommentId { get; set; } = DiscussionCommentId.From(Guid.Empty);
    public UserId ActingUserId { get; set; } = UserId.From(Guid.Empty);
    public bool IsWriterPlus { get; set; }
}
