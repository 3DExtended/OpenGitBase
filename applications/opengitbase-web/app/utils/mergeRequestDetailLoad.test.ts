import { describe, expect, it } from 'vitest'
import type {
  ApiResult,
  MergeRequest,
  MergeRequestChanges,
  MergeRequestComment,
  MergeRequestCommit,
  MergeRequestDiscussionLink,
} from './api'
import { resolveMergeRequestDetailLoad } from './mergeRequestDetailLoad'

const messages = {
  notFound: 'Merge request not found.',
  loadFailed: 'Could not load merge request details.',
}

function mr(): MergeRequest {
  return {
    id: 'mr-1',
    repositoryId: 'repo-1',
    number: 1,
    title: 'Test MR',
    body: null,
    status: 'Open',
    isDraft: false,
    creatorUserId: 'user-1',
    sourceRef: 'feature',
    targetRef: 'main',
    sourceHeadSha: 'abc',
    targetBaseSha: 'def',
    createdAt: '2026-01-01T00:00:00.000Z',
    updatedAt: '2026-01-01T00:00:00.000Z',
  }
}

function ok<T>(data: T): ApiResult<T> {
  return { data, error: null, status: 200 }
}

function fail<T>(status: number, error: string): ApiResult<T> {
  return { data: null, error, status }
}

describe('resolveMergeRequestDetailLoad', () => {
  it('returns fatal error when MR fetch fails', () => {
    const result = resolveMergeRequestDetailLoad({
      mrResult: fail(404, 'Not found'),
      overviewResult: ok([]),
      reviewResult: ok([]),
      changesResult: ok({ files: [] }),
      commitsResult: ok([]),
      linksResult: ok([]),
      messages,
    })

    expect(result.mr).toBeNull()
    expect(result.error).toBe('Not found')
    expect(result.partialLoadError).toBeNull()
  })

  it('surfaces partial load errors when discussion links fail', () => {
    const result = resolveMergeRequestDetailLoad({
      mrResult: ok(mr()),
      overviewResult: ok([]),
      reviewResult: ok([]),
      changesResult: ok({ files: [] }),
      commitsResult: ok([]),
      linksResult: fail(500, 'Discussion links unavailable'),
      messages,
    })

    expect(result.mr?.number).toBe(1)
    expect(result.error).toBeNull()
    expect(result.partialLoadError).toBe('Discussion links unavailable')
    expect(result.linkedDiscussions).toEqual([])
  })
})
