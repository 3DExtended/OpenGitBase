import type { RouteLocationRaw } from 'vue-router'
import { commentElementId, normalizeCommentId } from './discussionCommentHash'

function normalizeSlug(value: string): string {
  return value.trim().replace(/^\/+|\/+$/g, '')
}

/** In-app route to a discussion (optional comment hash). Returns null when slugs are invalid. */
export function discussionDetailRoute(
  owner: string,
  repo: string,
  number: number,
  options?: { commentId?: string | null },
): RouteLocationRaw | null {
  const ownerSlug = normalizeSlug(owner)
  const repoSlug = normalizeSlug(repo)
  if (!ownerSlug || !repoSlug || !Number.isFinite(number) || number < 1) {
    return null
  }

  const path = `/${ownerSlug}/${repoSlug}/discussions/${number}`
  const commentId = options?.commentId ? normalizeCommentId(options.commentId) : null
  if (!commentId) {
    return path
  }

  return {
    path,
    hash: `#${commentElementId(commentId)}`,
    query: { comment: commentId },
  }
}
