namespace OpenGitBase.Cli;

/// <summary>
/// One part of a published spec and the comment that holds it. <see cref="CommentId"/> is null only for a
/// discussion body the API never copied into an opening comment; such a part cannot be edited.
/// </summary>
public sealed record DiscussionSpecPart(string Text, Guid? CommentId);
