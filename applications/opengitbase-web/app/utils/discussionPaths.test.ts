import { describe, expect, it } from 'vitest'
import { discussionDetailRoute } from './discussionPaths'

describe('discussionDetailRoute', () => {
  it('builds a discussion route with hash and query fallback', () => {
    expect(discussionDetailRoute('owner', 'repo', 3, {
      commentId: '550E8400-E29B-41d4-A716-446655440000',
    })).toEqual({
      path: '/owner/repo/discussions/3',
      hash: '#comment-550e8400-e29b-41d4-a716-446655440000',
      query: { comment: '550e8400-e29b-41d4-a716-446655440000' },
    })
  })

  it('returns null for invalid slugs', () => {
    expect(discussionDetailRoute('', 'repo', 1)).toBeNull()
  })
})
