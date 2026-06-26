import { describe, expect, it } from 'vitest'
import { normalizeDiscussion, normalizeDiscussionComment } from './api'

describe('normalizeDiscussionComment', () => {
  it('normalizes production-shaped comment with wrapped ids and nested replies', () => {
    const raw = {
      id: { value: '4e4bf53e-98c5-439e-81c3-19cc009ae0f9' },
      discussionId: { value: '235c039b-315a-4880-b4f3-1c935688a69c' },
      authorUserId: { value: '25821f40-4f03-4566-b977-d87e44c00a73' },
      bodyMarkdown: 'asd',
      createdAt: '2026-06-22T12:24:39.620652+00:00',
      updatedAt: '2026-06-22T12:24:39.620652+00:00',
      isDeleted: false,
      parentCommentId: null,
      isResolved: false,
      replyCount: 1,
      orphanedFromDeletedRoot: false,
      replies: [
        {
          id: { value: 'reply-id-1' },
          discussionId: { value: '235c039b-315a-4880-b4f3-1c935688a69c' },
          authorUserId: { value: 'other-user-id' },
          bodyMarkdown: 'nested reply',
          createdAt: '2026-06-22T12:25:00.000000+00:00',
          updatedAt: '2026-06-22T12:25:00.000000+00:00',
          isDeleted: false,
          isResolved: false,
          replyCount: 0,
          orphanedFromDeletedRoot: false,
          replies: [],
        },
      ],
      anchor: null,
    }

    const comment = normalizeDiscussionComment(raw)

    expect(comment.id).toBe('4e4bf53e-98c5-439e-81c3-19cc009ae0f9')
    expect(comment.bodyMarkdown).toBe('asd')
    expect(comment.replyCount).toBe(1)
    expect(comment.replies).toHaveLength(1)
    expect(comment.replies[0]?.bodyMarkdown).toBe('nested reply')
    expect(comment.replies[0]?.id).toBe('reply-id-1')
  })
})

describe('normalizeDiscussion', () => {
  it('normalizes bundled comments on detail GET', () => {
    const raw = {
      id: { value: '235c039b-315a-4880-b4f3-1c935688a69c' },
      repositoryId: '2f274a0f-53fc-4bd0-81af-44b1d94612cb',
      number: 1,
      title: 'Test',
      body: null,
      status: 0,
      hasEverBeenEngaged: false,
      creatorUserId: { value: '25821f40-4f03-4566-b977-d87e44c00a73' },
      createdAt: '2026-06-22T12:23:56.914753+00:00',
      updatedAt: '2026-06-26T08:08:26.608154+00:00',
      tags: [],
      comments: [
        {
          id: { value: 'comment-1' },
          discussionId: { value: '235c039b-315a-4880-b4f3-1c935688a69c' },
          authorUserId: { value: '25821f40-4f03-4566-b977-d87e44c00a73' },
          bodyMarkdown: 'first',
          createdAt: '2026-06-22T12:24:39.620652+00:00',
          updatedAt: '2026-06-22T12:24:39.620652+00:00',
          isDeleted: false,
          isResolved: false,
          replyCount: 0,
          orphanedFromDeletedRoot: false,
          replies: [],
        },
      ],
    }

    const discussion = normalizeDiscussion(raw)

    expect(discussion.status).toBe('Open')
    expect(discussion.comments).toHaveLength(1)
    expect(discussion.comments?.[0]?.bodyMarkdown).toBe('first')
  })
})
