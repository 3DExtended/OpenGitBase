import { afterEach, describe, expect, it, vi } from 'vitest'
import { createApi } from './api'

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

describe('discussions API client', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('requests bundled comments via include=comments query param', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      jsonResponse({
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
        comments: [
          {
            id: 'comment-1',
            discussionId: 'disc-1',
            authorUserId: 'user-1',
            bodyMarkdown: 'hello',
            createdAt: '2026-06-01T00:00:00.000Z',
            updatedAt: '2026-06-01T00:00:00.000Z',
            isDeleted: false,
            isResolved: false,
            replyCount: 0,
            orphanedFromDeletedRoot: false,
            replies: [],
          },
        ],
      }),
    )
    vi.stubGlobal('fetch', fetchMock)

    const api = createApi('/api')
    const result = await api.discussions.get('opengitbase', 'open-git-base', 1, {
      includeComments: true,
    })

    expect(fetchMock).toHaveBeenCalledOnce()
    expect(fetchMock.mock.calls[0]?.[0]).toBe(
      '/api/repository/by-slug/opengitbase/open-git-base/discussions/1?include=comments',
    )
    expect(result.status).toBe(200)
    expect(result.data?.comments).toHaveLength(1)
    expect(result.data?.comments?.[0]?.bodyMarkdown).toBe('hello')
  })

  it('does not hit /comments path when bundled comments are returned', async () => {
    const fetchMock = vi.fn().mockImplementation(async (url: string) => {
      if (url.includes('/comments')) {
        throw new TypeError('Failed to fetch')
      }
      return jsonResponse({
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
        comments: [
          {
            id: 'comment-1',
            discussionId: 'disc-1',
            authorUserId: 'user-1',
            bodyMarkdown: 'bundled only',
            createdAt: '2026-06-01T00:00:00.000Z',
            updatedAt: '2026-06-01T00:00:00.000Z',
            isDeleted: false,
            isResolved: false,
            replyCount: 0,
            orphanedFromDeletedRoot: false,
            replies: [],
          },
        ],
      })
    })
    vi.stubGlobal('fetch', fetchMock)

    const api = createApi('/api')
    const result = await api.discussions.get('opengitbase', 'open-git-base', 1, {
      includeComments: true,
    })

    expect(result.data?.comments).toHaveLength(1)
    expect(fetchMock.mock.calls.some(([url]) => String(url).includes('/comments'))).toBe(false)
  })
})
