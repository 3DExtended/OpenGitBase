import { describe, expect, it, vi } from 'vitest'
import type { ApiResult, Discussion, DiscussionComment } from './api'
import { resolveCommentsFallbackLoad, resolveDiscussionDetailLoad } from './discussionDetailLoad'

const errorMessage = "Couldn't load comments."

function discussion(overrides: Partial<Discussion> = {}): Discussion {
  return {
    id: 'disc-1',
    repositoryId: 'repo-1',
    number: 1,
    title: 'Test',
    status: 'Open',
    hasEverBeenEngaged: false,
    creatorUserId: 'user-1',
    createdAt: '2026-06-01T00:00:00.000Z',
    updatedAt: '2026-06-01T00:00:00.000Z',
    tags: [],
    ...overrides,
  }
}

function comment(id: string, body: string): DiscussionComment {
  return {
    id,
    discussionId: 'disc-1',
    authorUserId: 'user-1',
    bodyMarkdown: body,
    createdAt: '2026-06-01T00:00:00.000Z',
    updatedAt: '2026-06-01T00:00:00.000Z',
    isDeleted: false,
    isResolved: false,
    replyCount: 0,
    orphanedFromDeletedRoot: false,
    replies: [],
  }
}

function ok<T>(data: T): ApiResult<T> {
  return { data, error: null, status: 200 }
}

function fail<T>(status: number, error: string): ApiResult<T> {
  return { data: null, error, status }
}

describe('resolveDiscussionDetailLoad', () => {
  it('uses bundled comments without calling listComments (ad-blocker safe path)', async () => {
    const listComments = vi.fn()
    const bundled = [
      comment('c-1', 'first'),
      comment('c-2', 'second'),
    ]

    const result = await resolveDiscussionDetailLoad({
      getResult: ok(discussion({ comments: bundled })),
      listComments,
      commentsLoadErrorMessage: errorMessage,
    })

    expect(result.discussion?.title).toBe('Test')
    expect(result.comments).toHaveLength(2)
    expect(result.commentsError).toBeNull()
    expect(result.forbidden).toBe(false)
    expect(listComments).not.toHaveBeenCalled()
  })

  it('falls back to listComments when bundled comments are absent', async () => {
    const listComments = vi.fn().mockResolvedValue(
      ok([comment('c-1', 'from fallback')]),
    )

    const result = await resolveDiscussionDetailLoad({
      getResult: ok(discussion()),
      listComments,
      commentsLoadErrorMessage: errorMessage,
    })

    expect(result.comments).toHaveLength(1)
    expect(result.comments[0]?.bodyMarkdown).toBe('from fallback')
    expect(result.commentsError).toBeNull()
    expect(listComments).toHaveBeenCalledOnce()
  })

  it('sets commentsError when listComments fails but keeps discussion', async () => {
    const listComments = vi.fn().mockResolvedValue(
      fail<DiscussionComment[]>(0, 'Network error'),
    )

    const result = await resolveDiscussionDetailLoad({
      getResult: ok(discussion()),
      listComments,
      commentsLoadErrorMessage: errorMessage,
    })

    expect(result.discussion?.title).toBe('Test')
    expect(result.comments).toHaveLength(0)
    expect(result.commentsError).toBe('Network error')
  })

  it('returns forbidden on 403 without attempting comments', async () => {
    const listComments = vi.fn()

    const result = await resolveDiscussionDetailLoad({
      getResult: fail<Discussion>(403, 'Forbidden'),
      listComments,
      commentsLoadErrorMessage: errorMessage,
    })

    expect(result.forbidden).toBe(true)
    expect(result.discussion).toBeNull()
    expect(listComments).not.toHaveBeenCalled()
  })

  it('returns not-found state on 404', async () => {
    const result = await resolveDiscussionDetailLoad({
      getResult: fail<Discussion>(404, 'Not found'),
      listComments: vi.fn(),
      commentsLoadErrorMessage: errorMessage,
    })

    expect(result.discussion).toBeNull()
    expect(result.comments).toHaveLength(0)
    expect(result.commentsError).toBeNull()
    expect(result.forbidden).toBe(false)
  })
})

describe('resolveCommentsFallbackLoad', () => {
  it('returns comments on successful retry', async () => {
    const result = await resolveCommentsFallbackLoad({
      listComments: async () => ok([comment('c-1', 'retry')]),
      commentsLoadErrorMessage: errorMessage,
    })

    expect(result.comments).toHaveLength(1)
    expect(result.commentsError).toBeNull()
  })

  it('returns error when retry fails', async () => {
    const result = await resolveCommentsFallbackLoad({
      listComments: async () => fail(0, 'blocked'),
      commentsLoadErrorMessage: errorMessage,
    })

    expect(result.comments).toHaveLength(0)
    expect(result.commentsError).toBe('blocked')
  })
})
