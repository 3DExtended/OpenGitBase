import type {
  ApiResult,
  MergeRequest,
  MergeRequestChanges,
  MergeRequestComment,
  MergeRequestCommit,
  MergeRequestDiscussionLink,
} from '~/utils/api'

export type MergeRequestDetailLoadResult = {
  mr: MergeRequest | null
  overviewComments: MergeRequestComment[]
  reviewComments: MergeRequestComment[]
  changes: MergeRequestChanges | null
  commits: MergeRequestCommit[]
  linkedDiscussions: MergeRequestDiscussionLink[]
  error: string | null
  partialLoadError: string | null
}

export type MergeRequestDetailLoadMessages = {
  notFound: string
  loadFailed: string
}

function isFailedResult<T>(result: ApiResult<T>): boolean {
  return Boolean(result.error) || result.status >= 400
}

export function resolveMergeRequestDetailLoad(input: {
  mrResult: ApiResult<MergeRequest>
  overviewResult: ApiResult<MergeRequestComment[]>
  reviewResult: ApiResult<MergeRequestComment[]>
  changesResult: ApiResult<MergeRequestChanges>
  commitsResult: ApiResult<MergeRequestCommit[]>
  linksResult: ApiResult<MergeRequestDiscussionLink[]>
  messages: MergeRequestDetailLoadMessages
}): MergeRequestDetailLoadResult {
  const {
    mrResult,
    overviewResult,
    reviewResult,
    changesResult,
    commitsResult,
    linksResult,
    messages,
  } = input

  if (mrResult.error || !mrResult.data) {
    return {
      mr: null,
      overviewComments: [],
      reviewComments: [],
      changes: null,
      commits: [],
      linkedDiscussions: [],
      error: mrResult.error ?? messages.notFound,
      partialLoadError: null,
    }
  }

  const partialErrors: string[] = []
  if (isFailedResult(overviewResult)) {
    partialErrors.push(overviewResult.error ?? messages.loadFailed)
  }
  if (isFailedResult(reviewResult)) {
    partialErrors.push(reviewResult.error ?? messages.loadFailed)
  }
  if (isFailedResult(changesResult)) {
    partialErrors.push(changesResult.error ?? messages.loadFailed)
  }
  if (isFailedResult(commitsResult)) {
    partialErrors.push(commitsResult.error ?? messages.loadFailed)
  }
  if (isFailedResult(linksResult)) {
    partialErrors.push(linksResult.error ?? messages.loadFailed)
  }

  return {
    mr: mrResult.data,
    overviewComments: overviewResult.data ?? [],
    reviewComments: reviewResult.data ?? [],
    changes: changesResult.data ?? null,
    commits: commitsResult.data ?? [],
    linkedDiscussions: linksResult.data ?? [],
    error: null,
    partialLoadError: partialErrors.length ? partialErrors.join(' ') : null,
  }
}
