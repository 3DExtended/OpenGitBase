namespace OpenGitBase.Cli;

/// <summary>A spec part whose comment needs new text. <see cref="Index"/> is zero-based.</summary>
public sealed record DiscussionSpecPartEdit(int Index, Guid CommentId, string Text);
