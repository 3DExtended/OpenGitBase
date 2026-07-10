import { describe, expect, it } from 'vitest'
import { resolveReviewReplyAnchor } from './mergeRequestReviewReply'

describe('resolveReviewReplyAnchor', () => {
  it('returns null when no anchor is attached', () => {
    expect(resolveReviewReplyAnchor(null, 'old')).toBeNull()
  })

  it('uses the line diff side instead of hardcoding new', () => {
    expect(resolveReviewReplyAnchor({
      ref: 'main',
      commitSha: 'abc123',
      filePath: 'src/main.ts',
      line: 12,
    }, 'old')).toEqual({
      headCommitSha: 'abc123',
      filePath: 'src/main.ts',
      lineNumber: 12,
      diffSide: 'old',
    })
  })
})
