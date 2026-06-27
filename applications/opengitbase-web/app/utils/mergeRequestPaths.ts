import type { RouteLocationRaw } from 'vue-router'

function normalizeSlug(value: string): string {
  return value.trim().replace(/^\/+|\/+$/g, '')
}

export function mergeRequestDetailRoute(
  owner: string,
  repo: string,
  number: number,
): RouteLocationRaw | null {
  const ownerSlug = normalizeSlug(owner)
  const repoSlug = normalizeSlug(repo)
  if (!ownerSlug || !repoSlug || !Number.isFinite(number) || number < 1) {
    return null
  }
  return `/${ownerSlug}/${repoSlug}/merge-requests/${number}`
}
