using OpenGitBase.Cli.Api.Models;

namespace OpenGitBase.Cli;

/// <summary>
/// The spec a PRD, ADR, or slice discussion publishes: the discussion body followed by the creator's top-level
/// comments posted in the same publish burst (each within <see cref="PublishBurstGap"/> of the previous post).
/// The API echoes the body as the opening comment, and only comments are editable, so that echo replaces the
/// body when it matches or has been edited. Later comments (audits, status notes) and comments by anyone else
/// are discussion, not spec. <c>ogb docs pull</c> mirrors these parts; <c>ogb issue edit</c> rewrites them.
/// </summary>
public static class DiscussionSpec
{
    public static readonly TimeSpan PublishBurstGap = TimeSpan.FromMinutes(10);

    public static IReadOnlyList<DiscussionSpecPart> CollectParts(DiscussionModel discussion)
    {
        var parts = new List<DiscussionSpecPart>();
        var body = discussion.Body?.Trim() ?? string.Empty;
        if (body.Length > 0)
        {
            parts.Add(new DiscussionSpecPart(body, CommentId: null));
        }

        var rootComments = (discussion.Comments ?? [])
            .Where(comment => comment.ParentCommentId is null)
            .OrderBy(comment => comment.CreatedAt);

        var previousPostAt = discussion.CreatedAt;
        var isOpeningComment = true;
        foreach (var comment in rootComments)
        {
            if (!string.Equals(comment.AuthorUsername, discussion.CreatorUsername, StringComparison.Ordinal)
                || comment.CreatedAt - previousPostAt > PublishBurstGap)
            {
                break;
            }

            previousPostAt = comment.CreatedAt;
            var text = comment.BodyMarkdown.Trim();
            var isBodyEcho = isOpeningComment
                && body.Length > 0
                && (comment.EditedAt is not null || string.Equals(text, body, StringComparison.Ordinal));
            isOpeningComment = false;
            if (isBodyEcho)
            {
                parts[0] = new DiscussionSpecPart(text, comment.Id);
            }
            else if (text.Length > 0)
            {
                parts.Add(new DiscussionSpecPart(text, comment.Id));
            }
        }

        return parts;
    }

    /// <summary>
    /// Splits <paramref name="text"/> into comment-sized parts (the opening comment, unlike the body, holds up
    /// to <see cref="DiscussionBodySplitter.MaxCommentLength"/>) and pairs them, in order, with the spec's
    /// existing parts. Parts whose text is unchanged need no edit; parts a shorter text leaves over are surplus.
    /// The text cannot grow past the spec's part count: a comment posted now falls outside the publish burst,
    /// so the mirror would never include it.
    /// </summary>
    public static DiscussionSpecRewrite PlanRewrite(DiscussionModel discussion, string? text)
    {
        var parts = DiscussionBodySplitter.Split(text, firstPartLimit: DiscussionBodySplitter.MaxCommentLength);
        if (parts.Count == 0)
        {
            throw new InvalidOperationException("Spec text is required. Pass --body or --body-file.");
        }

        var current = CollectParts(discussion);
        if (current.Count > 0 && current[0].CommentId is null)
        {
            throw new InvalidOperationException(
                $"The spec of #{discussion.Number} starts with a discussion body that has no opening comment, and the "
                + "API cannot edit a discussion body. Publish the new text as a new issue and dismiss "
                + $"#{discussion.Number}.");
        }

        if (parts.Count > current.Count)
        {
            throw new InvalidOperationException(
                $"The new text needs {parts.Count} part(s), but the spec of #{discussion.Number} has {current.Count}. "
                + "A comment posted now would fall outside the publish burst and never reach the docs mirror. "
                + $"Shorten the text, or publish it as a new issue and dismiss #{discussion.Number}.");
        }

        var edits = parts
            .Select((part, index) => new DiscussionSpecPartEdit(index, current[index].CommentId!.Value, part))
            .Where(edit => !string.Equals(edit.Text, current[edit.Index].Text, StringComparison.Ordinal))
            .ToList();
        var surplusCommentIds = current.Skip(parts.Count).Select(part => part.CommentId!.Value).ToList();
        return new DiscussionSpecRewrite(parts.Count, edits, surplusCommentIds);
    }
}
