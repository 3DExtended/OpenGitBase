import { afterEach, describe, expect, it, vi } from 'vitest'
import { createApi } from './api'

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

describe('repository byte override API client', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('requests byte override eligibility', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      jsonResponse({
        eligible: true,
        reason: 'Eligible for per-repository byte override.',
        currentOverride: null,
        maxAllowedOverride: 10_737_418_240,
        orgContributedNodeCount: 5,
      }),
    )
    vi.stubGlobal('fetch', fetchMock)

    const api = createApi('/api')
    const result = await api.repositories.byteOverrideEligibility('repo-1')

    expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/repository/repo-1/byte-override-eligibility')
    expect(result.error).toBeNull()
    expect(result.data).toEqual({
      eligible: true,
      reason: 'Eligible for per-repository byte override.',
      currentOverride: null,
      maxAllowedOverride: 10_737_418_240,
      orgContributedNodeCount: 5,
    })
  })

  it('patches max bytes override', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      jsonResponse({
        id: 'repo-1',
        name: 'Demo',
        slug: 'demo',
        ownerUserId: 'org-1',
        isPrivate: false,
        maxBytesOverride: 5_368_709_120,
      }),
    )
    vi.stubGlobal('fetch', fetchMock)

    const api = createApi('/api')
    const result = await api.repositories.updateMaxBytesOverride('repo-1', {
      maxBytesOverride: 5_368_709_120,
    })

    expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/repository/repo-1/max-bytes-override')
    expect(fetchMock.mock.calls[0]?.[1]).toMatchObject({
      method: 'PATCH',
      body: JSON.stringify({ maxBytesOverride: 5_368_709_120 }),
    })
    expect(result.error).toBeNull()
    expect(result.data?.maxBytesOverride).toBe(5_368_709_120)
  })

  it('clears max bytes override with null', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      jsonResponse({
        id: 'repo-1',
        name: 'Demo',
        slug: 'demo',
        ownerUserId: 'org-1',
        isPrivate: false,
        maxBytesOverride: null,
      }),
    )
    vi.stubGlobal('fetch', fetchMock)

    const api = createApi('/api')
    await api.repositories.updateMaxBytesOverride('repo-1', { maxBytesOverride: null })

    expect(fetchMock.mock.calls[0]?.[1]).toMatchObject({
      method: 'PATCH',
      body: JSON.stringify({ maxBytesOverride: null }),
    })
  })

  it('normalizes maxBytesOverride on repository payloads', async () => {
    const fetchMock = vi.fn().mockResolvedValue(
      jsonResponse({
        id: '00000000-0000-0000-0000-000000000001',
        name: 'Demo',
        slug: 'demo',
        ownerUserId: '00000000-0000-0000-0000-000000000002',
        isPrivate: true,
        maxBytesOverride: '2147483648',
      }),
    )
    vi.stubGlobal('fetch', fetchMock)

    const api = createApi('/api')
    const result = await api.repositories.get('00000000-0000-0000-0000-000000000001')

    expect(result.data?.maxBytesOverride).toBe(2_147_483_648)
  })
})
