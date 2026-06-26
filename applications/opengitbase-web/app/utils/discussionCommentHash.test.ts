import { describe, expect, it } from 'vitest'
import type { DiscussionComment } from './api'
import {
  commentElementId,
  commentIdsMatch,
  normalizeCommentId,
  parseCommentIdFromHash,
  resolveTargetCommentId,
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

describe('normalizeCommentId', () => {
  it('normalizes wrapped API ids and lowercases guids', () => {
    expect(normalizeCommentId({
      value: '550E8400-E29B-41d4-A716-446655440000',
    })).toBe('550e8400-e29b-41d4-a716-446655440000')
  })

  it('rejects invalid values', () => {
    expect(normalizeCommentId({})).toBeNull()
    expect(normalizeCommentId('[object Object]')).toBeNull()
  })
})

describe('parseCommentIdFromHash', () => {
  it('parses comment ids from hash fragments', () => {
    expect(parseCommentIdFromHash('#comment-abc-123')).toBe('abc-123')
  })

  it('returns null for unrelated hashes', () => {
    expect(parseCommentIdFromHash('#top')).toBeNull()
    expect(parseCommentIdFromHash('')).toBeNull()
  })
})

describe('resolveTargetCommentId', () => {
  it('prefers hash and falls back to query', () => {
    expect(resolveTargetCommentId({
      hash: '#comment-from-hash',
      query: { comment: 'from-query' },
    })).toBe('from-hash')

    expect(resolveTargetCommentId({
      hash: '',
      query: { comment: 'from-query' },
    })).toBe('from-query')
  })
})

describe('commentElementId', () => {
  it('builds stable element ids', () => {
    expect(commentElementId('ABC-123')).toBe('comment-abc-123')
  })
})

describe('subtreeContainsComment', () => {
  it('matches root and reply comments case-insensitively', () => {
    const root = comment('ROOT-1', [comment('reply-1')])
    expect(subtreeContainsComment(root, 'root-1')).toBe(true)
    expect(subtreeContainsComment(root, 'REPLY-1')).toBe(true)
    expect(subtreeContainsComment(root, 'missing')).toBe(false)
  })
})

describe('commentIdsMatch', () => {
  it('compares ids case-insensitively', () => {
    expect(commentIdsMatch('ABC', 'abc')).toBe(true)
  })
})
