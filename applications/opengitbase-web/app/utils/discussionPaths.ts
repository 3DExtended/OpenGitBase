import type { RouteLocationRaw } from 'vue-router'

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
  if (options?.commentId) {
    return { path, hash: `#comment-${options.commentId}` }
  }
  return path
}
