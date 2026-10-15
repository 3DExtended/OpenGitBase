namespace OpenGitBase.Cli;

/// <summary>
/// The comment updates that rewrite a spec in place, and the trailing spec comments a shorter text no longer
/// needs (soft-deleted so the docs mirror drops them).
/// </summary>
public sealed record DiscussionSpecRewrite(
    int PartCount,
    IReadOnlyList<DiscussionSpecPartEdit> Edits,
    IReadOnlyList<Guid> SurplusCommentIds);
