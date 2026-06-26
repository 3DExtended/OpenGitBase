import type { LocationQuery } from 'vue-router'
import type { DiscussionComment } from '~/utils/api'

const COMMENT_HASH_PREFIX = '#comment-'

export function normalizeCommentId(raw: unknown): string | null {
  if (raw == null) {
    return null
  }

  let id: string | null = null

  if (typeof raw === 'string') {
    id = raw
  }
  else if (typeof raw === 'object') {
    const record = raw as Record<string, unknown>
    const nested = record.value ?? record.Value ?? record.id ?? record.Id
    if (nested != null) {
      return normalizeCommentId(nested)
    }
  }

  if (id == null) {
    return null
  }

  id = id.trim()
  if (!id || id === '[object Object]') {
    return null
  }

  return id.toLowerCase()
}

export function parseCommentIdFromHash(hash: string): string | null {
  if (!hash.startsWith(COMMENT_HASH_PREFIX)) {
    return null
  }
  return normalizeCommentId(hash.slice(COMMENT_HASH_PREFIX.length))
}

export function commentElementId(commentId: string): string {
  const normalized = normalizeCommentId(commentId)
  return `comment-${normalized ?? commentId}`
}

export function commentIdsMatch(left: string, right: string): boolean {
  const a = normalizeCommentId(left)
  const b = normalizeCommentId(right)
  return a != null && b != null && a === b
}

export function resolveTargetCommentId(input: {
  hash: string
  query: LocationQuery
}): string | null {
  const fromHash = parseCommentIdFromHash(input.hash)
  if (fromHash) {
    return fromHash
  }

  const queryValue = input.query.comment
  if (typeof queryValue === 'string') {
    return normalizeCommentId(queryValue)
  }
  if (Array.isArray(queryValue) && typeof queryValue[0] === 'string') {
    return normalizeCommentId(queryValue[0])
  }

  return null
}

export function subtreeContainsComment(
  comment: DiscussionComment,
  commentId: string,
): boolean {
  if (commentIdsMatch(comment.id, commentId)) {
    return true
  }
  return comment.replies?.some(reply => commentIdsMatch(reply.id, commentId)) ?? false
}

export function findCommentElement(commentId: string): HTMLElement | null {
  const normalized = normalizeCommentId(commentId)
  if (!normalized) {
    return null
  }

  const targetId = commentElementId(normalized)
  const direct = document.getElementById(targetId)
  if (direct) {
    return direct
  }

  for (const element of document.querySelectorAll('[id^="comment-"]')) {
    if (element.id.toLowerCase() === targetId) {
      return element as HTMLElement
    }
  }

  return null
}

export function scrollToCommentElement(
  commentId: string,
  options: {
    behavior?: ScrollBehavior
    maxAttempts?: number
    delayMs?: number
  } = {},
): void {
  if (!import.meta.client) {
    return
  }

  const { behavior = 'smooth', maxAttempts = 40, delayMs = 50 } = options

  const attempt = (count: number): void => {
    const element = findCommentElement(commentId)
    if (element) {
      element.scrollIntoView({ behavior, block: 'center' })
      if (element instanceof HTMLElement) {
        element.focus({ preventScroll: true })
      }
      return
    }
    if (count < maxAttempts) {
      window.setTimeout(() => attempt(count + 1), delayMs)
    }
  }

  attempt(0)
}

export function scrollToCommentFromHash(
  hash: string,
  options?: Parameters<typeof scrollToCommentElement>[1],
): void {
  const commentId = parseCommentIdFromHash(hash)
  if (!commentId) {
    return
  }
  scrollToCommentElement(commentId, options)
}
