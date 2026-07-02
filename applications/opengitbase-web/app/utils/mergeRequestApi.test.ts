import { afterEach, describe, expect, it, vi } from 'vitest'
import { createApi } from './api'

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

describe('merge request API client', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('requests overview comments with type query param', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse([]))
    vi.stubGlobal('fetch', fetchMock)

    const api = createApi('/api')
    await api.mergeRequests.listComments('acme', 'demo', 3, { type: 'overview' })

    expect(fetchMock.mock.calls[0]?.[0]).toBe(
      '/api/repository/by-slug/acme/demo/merge-requests/3/comments?type=overview',
    )
  })

  it('lists discussion links for a merge request', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse([
      {
        discussionNumber: 2,
        relationshipType: 'closes',
        discussionTitle: 'Fix login',
        discussionStatus: 'Open',
      },
    ]))
    vi.stubGlobal('fetch', fetchMock)

    const api = createApi('/api')
    const result = await api.mergeRequests.listDiscussionLinks('acme', 'demo', 4)

    expect(result.error).toBeNull()
    expect(result.data).toEqual([
      {
        discussionNumber: 2,
        relationshipType: 'closes',
        discussionTitle: 'Fix login',
        discussionStatus: 'Open',
      },
    ])
    expect(fetchMock.mock.calls[0]?.[0]).toBe(
      '/api/repository/by-slug/acme/demo/merge-requests/4/discussion-links',
    )
  })

  it('creates and deletes discussion links', async () => {
    const fetchMock = vi.fn().mockImplementation(async (url: string, init?: RequestInit) => {
      if (init?.method === 'POST') {
        return jsonResponse({
          discussionNumber: 7,
          relationshipType: 'implements',
          discussionTitle: 'API cleanup',
          discussionStatus: 'Open',
        }, 201)
      }
      if (init?.method === 'DELETE') {
        return new Response(null, { status: 204 })
      }
      return jsonResponse(null, 404)
    })
    vi.stubGlobal('fetch', fetchMock)

    const api = createApi('/api')
    const created = await api.mergeRequests.createDiscussionLink('acme', 'demo', 1, {
      discussionNumber: 7,
      relationshipType: 'implements',
    })
    const deleted = await api.mergeRequests.deleteDiscussionLink('acme', 'demo', 1, 7, 'implements')

    expect(created.error).toBeNull()
    expect(created.data?.discussionNumber).toBe(7)
    expect(deleted.error).toBeNull()
    expect(fetchMock.mock.calls[0]?.[0]).toBe(
      '/api/repository/by-slug/acme/demo/merge-requests/1/discussion-links',
    )
    expect(fetchMock.mock.calls[1]?.[0]).toBe(
      '/api/repository/by-slug/acme/demo/merge-requests/1/discussion-links/7?relationshipType=implements',
    )
  })

  it('resolves repository id for protected branch rules', async () => {
    const fetchMock = vi.fn().mockImplementation(async (url: string) => {
      if (url.endsWith('/repository/by-slug/acme/demo')) {
        return jsonResponse({ id: '11111111-1111-1111-1111-111111111111' })
      }
      if (url.includes('/protected-branch-rules')) {
        return jsonResponse([])
      }
      return jsonResponse(null, 404)
    })
    vi.stubGlobal('fetch', fetchMock)

    const api = createApi('/api')
    const result = await api.repositorySettings.listProtectedBranchRules('acme', 'demo')

    expect(result.error).toBeNull()
    expect(result.data).toEqual([])
    expect(fetchMock.mock.calls.some(([url]) =>
      String(url).includes('/repository/11111111-1111-1111-1111-111111111111/protected-branch-rules'),
    )).toBe(true)
  })
})
