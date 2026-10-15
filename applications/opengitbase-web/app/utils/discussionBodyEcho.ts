import type { Discussion, DiscussionComment } from './api'

/** How long after a discussion opens its creator's first comment can still be the body echo. */
export const DISCUSSION_BODY_ECHO_WINDOW_MS = 10 * 60 * 1000

/**
 * The API copies a new discussion's body into an opening comment by its creator, and only that comment stays
 * editable. Returns that comment when it is still recognisable as the echo (it repeats the body or has been
 * edited since), so the page can treat it as the live text and skip the stored body, which goes stale after an
 * edit. Same rule as `DiscussionSpec.CollectParts` in the `ogb` CLI.
 */
export function findDiscussionBodyEcho(
  discussion: Discussion,
  comments: readonly DiscussionComment[],
): DiscussionComment | null {
  const body = discussion.body?.trim() ?? ''
  const opening = comments
    .filter(comment => !comment.parentCommentId)
    .sort((a, b) => Date.parse(a.createdAt) - Date.parse(b.createdAt))[0]
  if (!body || !opening || opening.authorUserId !== discussion.creatorUserId) {
    return null
  }

  const postedAfterOpening = Date.parse(opening.createdAt) - Date.parse(discussion.createdAt)
  if (!Number.isFinite(postedAfterOpening) || postedAfterOpening > DISCUSSION_BODY_ECHO_WINDOW_MS) {
    return null
  }
  return opening.editedAt || opening.bodyMarkdown.trim() === body ? opening : null
}

/** Whether the page renders the stored body on its own, above the comments. */
export function shouldShowDiscussionBody(
  discussion: Discussion,
  comments: readonly DiscussionComment[],
): boolean {
  return Boolean(discussion.body?.trim()) && !findDiscussionBodyEcho(discussion, comments)
}
