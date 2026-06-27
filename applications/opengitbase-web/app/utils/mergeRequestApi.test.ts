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
