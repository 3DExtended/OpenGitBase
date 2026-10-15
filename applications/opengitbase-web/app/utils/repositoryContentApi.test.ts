import { afterEach, describe, expect, it, vi } from 'vitest'
import { createApi } from './api'

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

describe('repository content API client', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('keeps when each tree entry was last modified', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(
      jsonResponse({
        ref: 'main',
        path: '',
        entries: [
          { name: 'src', path: 'src', type: 'tree', size: null, lastModifiedAt: '2024-06-01T12:00:00+00:00' },
          { name: 'README.md', path: 'README.md', type: 'blob', size: 12, lastModifiedAt: null },
          { name: 'LICENSE', path: 'LICENSE', type: 'blob', size: 4 },
        ],
        replicationLag: null,
      }),
    ))

    const api = createApi('/api')
    const result = await api.repositoryContent.getTree('owner', 'repo', 'main')

    expect(result.data?.entries.map(entry => [entry.name, entry.lastModifiedAt])).toEqual([
      ['src', '2024-06-01T12:00:00+00:00'],
      ['README.md', null],
      ['LICENSE', null],
    ])
  })
})
