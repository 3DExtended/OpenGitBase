import { describe, expect, it } from 'vitest'
import type { DiscussionComment } from './api'
import {
  commentElementId,
  parseCommentIdFromHash,
  subtreeContainsComment,
} from './discussionCommentHash'

function comment(id: string, replies: DiscussionComment[] = []): DiscussionComment {
  return {
    id,
    discussionId: 'disc-1',
    authorUserId: 'user-1',
    bodyMarkdown: 'body',
    createdAt: '2026-06-01T00:00:00.000Z',
    updatedAt: '2026-06-01T00:00:00.000Z',
    isDeleted: false,
    isResolved: false,
    replyCount: replies.length,
    orphanedFromDeletedRoot: false,
    replies,
  }
}

describe('parseCommentIdFromHash', () => {
  it('parses comment ids from hash fragments', () => {
    expect(parseCommentIdFromHash('#comment-abc-123')).toBe('abc-123')
  })

  it('returns null for unrelated hashes', () => {
    expect(parseCommentIdFromHash('#top')).toBeNull()
    expect(parseCommentIdFromHash('')).toBeNull()
  })
})

describe('commentElementId', () => {
  it('builds stable element ids', () => {
    expect(commentElementId('abc-123')).toBe('comment-abc-123')
  })
})

describe('subtreeContainsComment', () => {
  it('matches root and reply comments', () => {
    const root = comment('root-1', [comment('reply-1')])
    expect(subtreeContainsComment(root, 'root-1')).toBe(true)
    expect(subtreeContainsComment(root, 'reply-1')).toBe(true)
    expect(subtreeContainsComment(root, 'missing')).toBe(false)
  })
})
