import type { ApiResult, RepositoryCommit } from '~/utils/api'

export type CommitPageLoadState = {
  commit: RepositoryCommit | null
  forbidden: boolean
  unavailable: boolean
  notFound: boolean
  error: string | null
}

export type CommitPageLoadMessages = {
  notFound: string
  generic: string
}

export function resolveCommitPageLoad(
  result: ApiResult<RepositoryCommit>,
  messages: CommitPageLoadMessages,
): CommitPageLoadState {
  if (result.status === 403) {
    return {
      commit: null,
      forbidden: true,
      unavailable: false,
      notFound: false,
      error: null,
    }
  }

  if (result.status === 503) {
    return {
      commit: null,
      forbidden: false,
      unavailable: true,
      notFound: false,
      error: null,
    }
  }

  if (result.status === 404) {
    return {
      commit: null,
      forbidden: false,
      unavailable: false,
      notFound: true,
      error: messages.notFound,
    }
  }

  if (result.error || !result.data) {
    return {
      commit: null,
      forbidden: false,
      unavailable: false,
      notFound: false,
      error: result.error ?? messages.generic,
    }
  }

  return {
    commit: result.data,
    forbidden: false,
    unavailable: false,
    notFound: false,
    error: null,
  }
}
