using OpenGitBase.Cqrs.EfCore;

namespace OpenGitBase.Features.Discussion.Entities;

public class CommentAnchorEntity : IIdentifiableEntity<Guid>
{
    public Guid Id { get; set; }
    public Guid CommentId { get; set; }
    public string Ref { get; set; } = string.Empty;
    public string CommitSha { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int Line { get; set; }
    public int? EndLine { get; set; }

    public DiscussionCommentEntity? Comment { get; set; }
}
