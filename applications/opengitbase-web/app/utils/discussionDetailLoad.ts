import type { ApiResult, Discussion, DiscussionComment } from '~/utils/api'

export type DiscussionDetailLoadState = {
  discussion: Discussion | null
  comments: DiscussionComment[]
  forbidden: boolean
  commentsError: string | null
}

export async function resolveDiscussionDetailLoad(input: {
  getResult: ApiResult<Discussion>
  listComments: () => Promise<ApiResult<DiscussionComment[]>>
  commentsLoadErrorMessage: string
}): Promise<DiscussionDetailLoadState> {
  const { getResult, listComments, commentsLoadErrorMessage } = input

  if (getResult.status === 403) {
    return {
      discussion: null,
      comments: [],
      forbidden: true,
      commentsError: null,
    }
  }

  if (getResult.status === 404) {
    return {
      discussion: null,
      comments: [],
      forbidden: false,
      commentsError: null,
    }
  }

  if (getResult.status !== 200 || !getResult.data) {
    return {
      discussion: null,
      comments: [],
      forbidden: false,
      commentsError: getResult.error ?? commentsLoadErrorMessage,
    }
  }

  const discussion = getResult.data

  if (Array.isArray(discussion.comments)) {
    return {
      discussion,
      comments: discussion.comments,
      forbidden: false,
      commentsError: null,
    }
  }

  const commentsResult = await listComments()
  if (commentsResult.status === 200 && Array.isArray(commentsResult.data)) {
    return {
      discussion,
      comments: commentsResult.data,
      forbidden: false,
      commentsError: null,
    }
  }

  return {
    discussion,
    comments: [],
    forbidden: false,
    commentsError: commentsResult.error ?? commentsLoadErrorMessage,
  }
}

export async function resolveCommentsFallbackLoad(input: {
  listComments: () => Promise<ApiResult<DiscussionComment[]>>
  commentsLoadErrorMessage: string
}): Promise<Pick<DiscussionDetailLoadState, 'comments' | 'commentsError'>> {
  const commentsResult = await input.listComments()
  if (commentsResult.status === 200 && Array.isArray(commentsResult.data)) {
    return {
      comments: commentsResult.data,
      commentsError: null,
    }
  }

  return {
    comments: [],
    commentsError: commentsResult.error ?? input.commentsLoadErrorMessage,
  }
}
