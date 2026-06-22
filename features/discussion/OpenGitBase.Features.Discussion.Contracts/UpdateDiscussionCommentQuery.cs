using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Discussion.Contracts;

public class UpdateDiscussionCommentQuery : IQuery<DiscussionCommentDto, UpdateDiscussionCommentQuery>
{
    public Guid CommentId { get; set; }
    public UserId ActingUserId { get; set; } = UserId.From(Guid.Empty);
    public string BodyMarkdown { get; set; } = string.Empty;
}
