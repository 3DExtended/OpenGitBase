import type { DiscussionComment } from '~/utils/api'

const COMMENT_HASH_PREFIX = '#comment-'

export function parseCommentIdFromHash(hash: string): string | null {
  if (!hash.startsWith(COMMENT_HASH_PREFIX)) {
    return null
  }
  const commentId = hash.slice(COMMENT_HASH_PREFIX.length)
  return commentId.length > 0 ? commentId : null
}

export function commentElementId(commentId: string): string {
  return `comment-${commentId}`
}

export function subtreeContainsComment(
  comment: DiscussionComment,
  commentId: string,
): boolean {
  if (comment.id === commentId) {
    return true
  }
  return comment.replies?.some(reply => reply.id === commentId) ?? false
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

  const { behavior = 'smooth', maxAttempts = 30, delayMs = 50 } = options
  const elementId = commentElementId(commentId)

  const attempt = (count: number): void => {
    const element = document.getElementById(elementId)
    if (element) {
      element.scrollIntoView({ behavior, block: 'center' })
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
