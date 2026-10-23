import { describe, expect, it } from 'vitest'
import type { Discussion, DiscussionComment } from './api'
import { findDiscussionBodyEcho, resolveDiscussionOpening } from './discussionBodyEcho'

const creatorUserId = 'user-creator'
const openedAt = '2026-06-24T10:00:00.000Z'

function discussion(body: string | null): Discussion {
  return {
    id: 'disc-1',
    repositoryId: 'repo-1',
    number: 7,
    title: 'Spec',
    body,
    status: 'Open',
    hasEverBeenEngaged: false,
    creatorUserId,
    createdAt: openedAt,
    updatedAt: openedAt,
    tags: [],
  }
}

function comment(id: string, bodyMarkdown: string, overrides: Partial<DiscussionComment> = {}): DiscussionComment {
  return {
    id,
    discussionId: 'disc-1',
    authorUserId: creatorUserId,
    bodyMarkdown,
    createdAt: openedAt,
    updatedAt: openedAt,
    editedAt: null,
    isDeleted: false,
    parentCommentId: null,
    isResolved: false,
    replyCount: 0,
    orphanedFromDeletedRoot: false,
    replies: [],
    ...overrides,
  }
}

describe('findDiscussionBodyEcho', () => {
  it('returns the opening comment once its author has edited it', () => {
    const opening = comment('c-1', 'Revised spec', { editedAt: '2026-06-25T09:00:00.000Z' })

    expect(findDiscussionBodyEcho(discussion('Original spec'), [opening])).toBe(opening)
  })

  it('returns an unedited opening comment that repeats the body, ignoring surrounding whitespace', () => {
    const opening = comment('c-1', 'Original spec\n')

    expect(findDiscussionBodyEcho(discussion('  Original spec'), [opening])).toBe(opening)
  })

  it('ignores an unedited opening comment whose text differs from the body', () => {
    const opening = comment('c-1', 'A different first remark')

    expect(findDiscussionBodyEcho(discussion('Original spec'), [opening])).toBeNull()
  })

  it('ignores an opening comment by someone other than the creator', () => {
    const opening = comment('c-1', 'Original spec', { authorUserId: 'user-reviewer' })

    expect(findDiscussionBodyEcho(discussion('Original spec'), [opening])).toBeNull()
  })

  it('accepts an opening comment posted up to ten minutes after the discussion opened', () => {
    const opening = comment('c-1', 'Original spec', { createdAt: '2026-06-24T10:10:00.000Z' })

    expect(findDiscussionBodyEcho(discussion('Original spec'), [opening])).toBe(opening)
  })

  it('ignores an opening comment posted more than ten minutes after the discussion opened', () => {
    const opening = comment('c-1', 'Original spec', { createdAt: '2026-06-24T10:10:00.001Z' })

    expect(findDiscussionBodyEcho(discussion('Original spec'), [opening])).toBeNull()
  })

  it('judges only the earliest top-level comment, skipping orphaned replies', () => {
    const later = comment('c-2', 'Original spec', { createdAt: '2026-06-24T10:02:00.000Z' })
    const orphan = comment('c-3', 'Original spec', {
      createdAt: '2026-06-24T09:59:00.000Z',
      parentCommentId: 'c-deleted',
      orphanedFromDeletedRoot: true,
    })
    const opening = comment('c-1', 'Someone else spoke first', {
      authorUserId: 'user-reviewer',
      createdAt: '2026-06-24T10:01:00.000Z',
    })

    expect(findDiscussionBodyEcho(discussion('Original spec'), [orphan, later, opening])).toBeNull()
  })

  it('finds nothing to echo when the discussion has no body', () => {
    const opening = comment('c-1', '', { editedAt: '2026-06-25T09:00:00.000Z' })

    expect(findDiscussionBodyEcho(discussion(null), [opening])).toBeNull()
    expect(findDiscussionBodyEcho(discussion('  \n'), [opening])).toBeNull()
  })
})

describe('resolveDiscussionOpening', () => {
  it('opens with the echo comment and lists the remaining comments without it', () => {
    const opening = comment('c-1', 'Revised spec', { editedAt: '2026-06-25T09:00:00.000Z' })
    const remark = comment('c-2', 'Looks good', {
      authorUserId: 'user-reviewer',
      createdAt: '2026-06-24T11:00:00.000Z',
    })
    const later = comment('c-3', 'Follow-up', { createdAt: '2026-06-24T12:00:00.000Z' })

    expect(resolveDiscussionOpening(discussion('Original spec'), [opening, remark, later])).toEqual({
      body: null,
      comment: opening,
      comments: [remark, later],
    })
  })

  it('opens with the stored body when no comment echoes it', () => {
    const remark = comment('c-1', 'First!', { authorUserId: 'user-reviewer' })

    expect(resolveDiscussionOpening(discussion('Original spec'), [remark])).toEqual({
      body: 'Original spec',
      comment: null,
      comments: [remark],
    })
  })

  it('has no opening for a blank body', () => {
    const remark = comment('c-1', 'First!')

    expect(resolveDiscussionOpening(discussion(' \n'), [remark])).toEqual({
      body: null,
      comment: null,
      comments: [remark],
    })
  })
})
