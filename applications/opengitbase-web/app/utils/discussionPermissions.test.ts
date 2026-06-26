import { describe, expect, it } from 'vitest'
import type { DiscussionComment } from './api'
import {
  canResolveDiscussionSubThread,
  parseRepositoryRole,
  resolveEffectiveRepositoryRole,
  RepositoryRole,
} from './discussionPermissions'

function comment(overrides: Partial<DiscussionComment> = {}): DiscussionComment {
  return {
    id: 'comment-1',
    discussionId: 'disc-1',
    authorUserId: 'user-1',
    authorUsername: 'alice',
    bodyMarkdown: 'hello',
    createdAt: '2026-06-01T00:00:00.000Z',
    updatedAt: '2026-06-01T00:00:00.000Z',
    isDeleted: false,
    isResolved: false,
    replyCount: 0,
    orphanedFromDeletedRoot: false,
    replies: [],
    ...overrides,
  }
}

describe('parseRepositoryRole', () => {
  it('parses numeric and string roles', () => {
    expect(parseRepositoryRole(3)).toBe(RepositoryRole.Admin)
    expect(parseRepositoryRole('Writer')).toBe(RepositoryRole.Writer)
    expect(parseRepositoryRole('bogus')).toBe(RepositoryRole.None)
  })
})

describe('resolveEffectiveRepositoryRole', () => {
  it('prefers API viewer role over members list', () => {
    expect(resolveEffectiveRepositoryRole({
      viewerEffectiveRole: RepositoryRole.Writer,
      username: 'alice',
      members: [{ id: 'm-1', repositoryId: 'r-1', userId: 'u-1', username: 'alice', role: RepositoryRole.Reader }],
    })).toBe(RepositoryRole.Writer)
  })

  it('falls back to personal repo owner slug', () => {
    expect(resolveEffectiveRepositoryRole({
      username: 'alice',
      repo: { ownerSlug: 'alice', ownerKind: 'user' },
    })).toBe(RepositoryRole.Owner)
  })
})

describe('canResolveDiscussionSubThread', () => {
  it('allows comment author by user id even without members list', () => {
    expect(canResolveDiscussionSubThread({
      comment: comment({ authorUserId: 'user-1', authorUsername: 'alice' }),
      userId: 'user-1',
      username: 'alice',
      effectiveRole: RepositoryRole.Reader,
    })).toBe(true)
  })

  it('allows comment author by username match', () => {
    expect(canResolveDiscussionSubThread({
      comment: comment({ authorUserId: 'user-1', authorUsername: 'Alice' }),
      username: 'alice',
      effectiveRole: RepositoryRole.Reader,
    })).toBe(true)
  })

  it('allows writer plus on other authors threads', () => {
    expect(canResolveDiscussionSubThread({
      comment: comment({ authorUserId: 'user-2', authorUsername: 'bob' }),
      userId: 'user-1',
      username: 'alice',
      effectiveRole: RepositoryRole.Writer,
    })).toBe(true)
  })

  it('denies readers on other authors threads', () => {
    expect(canResolveDiscussionSubThread({
      comment: comment({ authorUserId: 'user-2', authorUsername: 'bob' }),
      userId: 'user-1',
      username: 'alice',
      effectiveRole: RepositoryRole.Reader,
    })).toBe(false)
  })
})
