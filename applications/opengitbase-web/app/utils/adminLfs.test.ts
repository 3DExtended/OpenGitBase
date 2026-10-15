import { afterEach, describe, expect, it, vi } from 'vitest'
import { createApi } from './api'
import type { AdminRepositoryLfsStatusDto } from './api'
import { lfsCopyPercent, lfsRestoreProgress } from './adminLfs'

function status(overrides: Partial<AdminRepositoryLfsStatusDto> = {}): AdminRepositoryLfsStatusDto {
  return {
    objectCount: 3,
    totalBytes: 3_000,
    missingCopies: 0,
    nodes: [
      { storageNodeId: 'n1', nodeId: 'storage-1', kind: 'Plaintext', isPrimary: true, copies: 3, missing: 0 },
      { storageNodeId: 'n3', nodeId: 'storage-3', kind: 'Encrypted', isPrimary: false, copies: 3, missing: 0 },
    ],
    ...overrides,
  }
}

function jsonResponse(body: unknown): Response {
  return new Response(JSON.stringify(body), { status: 200, headers: { 'Content-Type': 'application/json' } })
}

describe('lfsCopyPercent', () => {
  it('is complete for a repository without LFS objects', () => {
    expect(lfsCopyPercent(0, 0)).toBe(100)
  })

  it('never shows 100 while a copy is missing', () => {
    expect(lfsCopyPercent(199, 200)).toBe(99)
    expect(lfsCopyPercent(1, 3)).toBe(33)
    expect(lfsCopyPercent(3, 3)).toBe(100)
  })
})

describe('lfsRestoreProgress', () => {
  it('is null while the primary holds every object', () => {
    expect(lfsRestoreProgress(status())).toBeNull()
  })

  it('reports how many objects the primary has back', () => {
    const restoring = status({
      missingCopies: 2,
      nodes: [
        { storageNodeId: 'n4', nodeId: 'storage-4', kind: 'Plaintext', isPrimary: true, copies: 1, missing: 2 },
        { storageNodeId: 'n3', nodeId: 'storage-3', kind: 'Encrypted', isPrimary: false, copies: 3, missing: 0 },
      ],
    })

    expect(lfsRestoreProgress(restoring)).toEqual({ nodeId: 'storage-4', restored: 1, total: 3, percent: 33 })
  })

  it('is null when an encrypted replica misses copies but the primary does not', () => {
    const lagging = status({
      missingCopies: 1,
      nodes: [
        { storageNodeId: 'n1', nodeId: 'storage-1', kind: 'Plaintext', isPrimary: true, copies: 3, missing: 0 },
        { storageNodeId: 'n3', nodeId: 'storage-3', kind: 'Encrypted', isPrimary: false, copies: 2, missing: 1 },
      ],
    })

    expect(lfsRestoreProgress(lagging)).toBeNull()
  })
})

describe('admin replication API and LFS', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('reads the LFS figures of the repository detail', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(jsonResponse({
      repositoryId: 'r1',
      name: 'repo',
      slug: 'repo',
      ownerSlug: 'acme',
      replicationState: 'Rf4Healthy',
      primaryWatermark: 3,
      replicationEpoch: 1,
      writeQuorumAvailable: true,
      replicas: [],
      lfs: status(),
    })))

    const result = await createApi('/api').admin.replication.getRepository('r1')

    expect(result.data?.lfs).toEqual(status())
  })

  it('reads the fleet LFS summary', async () => {
    const fetchMock = vi.fn().mockResolvedValue(jsonResponse({
      objectCount: 12,
      totalBytes: 9_000_000,
      missingCopies: 4,
      repositoriesWithMissingCopies: 2,
    }))
    vi.stubGlobal('fetch', fetchMock)

    const result = await createApi('/api').admin.replication.getLfsSummary()

    expect(String(fetchMock.mock.calls[0]?.[0])).toContain('/api/admin/lfs/summary')
    expect(result.data).toEqual({ objectCount: 12, totalBytes: 9_000_000, missingCopies: 4, repositoriesWithMissingCopies: 2 })
  })
})
