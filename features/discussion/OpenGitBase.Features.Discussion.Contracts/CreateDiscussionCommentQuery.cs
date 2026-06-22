using OpenGitBase.Cqrs;
using OpenGitBase.Features.Users.Contracts.Models;

namespace OpenGitBase.Features.Discussion.Contracts;

public class CreateDiscussionCommentQuery : IQuery<DiscussionCommentDto, CreateDiscussionCommentQuery>
{
    public Guid RepositoryId { get; set; }
    public int DiscussionNumber { get; set; }
    public UserId AuthorUserId { get; set; } = UserId.From(Guid.Empty);
    public string BodyMarkdown { get; set; } = string.Empty;
    public CommentAnchorInput? Anchor { get; set; }
}
