import { describe, expect, it } from 'vitest'
import type { ApiResult, RepositoryCommit } from './api'
import { resolveCommitPageLoad } from './commitPageLoad'

const messages = {
  notFound: 'Commit not found.',
  generic: 'Could not load commit.',
}

function commit(overrides: Partial<RepositoryCommit> = {}): RepositoryCommit {
  return {
    sha: 'abc123',
    shortSha: 'abc123',
    message: 'Initial commit',
    authorName: 'Alice',
    authoredAt: '2026-01-01T00:00:00.000Z',
    parents: [],
    stats: null,
    kind: 'diff',
    diffFiles: [],
    rootFiles: [],
    replicationLag: null,
    ...overrides,
  }
}

function ok(data: RepositoryCommit): ApiResult<RepositoryCommit> {
  return { data, error: null, status: 200 }
}

function fail(status: number, error: string | null = 'Error'): ApiResult<RepositoryCommit> {
  return { data: null, error, status }
}

describe('resolveCommitPageLoad', () => {
  it('returns forbidden on 403', () => {
    const result = resolveCommitPageLoad(fail(403, 'Forbidden'), messages)
    expect(result.forbidden).toBe(true)
    expect(result.unavailable).toBe(false)
    expect(result.commit).toBeNull()
  })

  it('returns unavailable on 503', () => {
    const result = resolveCommitPageLoad(fail(503, 'Unavailable'), messages)
    expect(result.unavailable).toBe(true)
    expect(result.forbidden).toBe(false)
    expect(result.commit).toBeNull()
  })

  it('returns notFound on 404', () => {
    const result = resolveCommitPageLoad(fail(404, 'Not found'), messages)
    expect(result.notFound).toBe(true)
    expect(result.error).toBe(messages.notFound)
  })

  it('returns commit with replication lag on success', () => {
    const result = resolveCommitPageLoad(ok(commit({
      replicationLag: { behind: true, message: 'Syncing' },
    })), messages)
    expect(result.commit?.replicationLag).toEqual({ behind: true, message: 'Syncing' })
    expect(result.error).toBeNull()
  })
})
