#pragma warning disable SA1402 // File may only contain a single type
namespace OpenGitBase.Api.Models;

public sealed class CreateDiscussionRequest
{
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public Guid? AssigneeUserId { get; set; }
    public IReadOnlyList<Guid> TagIds { get; set; } = [];
    public CommentAnchorRequest? Anchor { get; set; }
}

public sealed class UpdateDiscussionRequest
{
    public string? Title { get; set; }
    public Guid? AssigneeUserId { get; set; }
    public bool ClearAssignee { get; set; }
    public IReadOnlyList<Guid>? TagIds { get; set; }
}

public sealed class CreateDiscussionCommentRequest
{
    public string BodyMarkdown { get; set; } = string.Empty;
    public Guid? ParentCommentId { get; set; }
    public CommentAnchorRequest? Anchor { get; set; }
}

public sealed class UpdateDiscussionCommentRequest
{
    public string BodyMarkdown { get; set; } = string.Empty;
}

public sealed class CommentAnchorRequest
{
    public string Ref { get; set; } = string.Empty;
    public string CommitSha { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public int Line { get; set; }
    public int? EndLine { get; set; }
}

public sealed class CreateRepositoryTagRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Color { get; set; }
}

public sealed class BlockRepositoryUserRequest
{
    public Guid UserId { get; set; }
    public string? Reason { get; set; }
}
