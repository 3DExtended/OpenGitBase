import type {
  CommentAnchorInput,
  Discussion,
  DiscussionComment,
  DiscussionDiscussionLink,
  DiscussionLinkedMergeRequest,
  DiscussionLinkType,
  DiscussionStatus,
  RepositoryMember,
} from '~/utils/api'
import { resolveCommentsFallbackLoad, resolveDiscussionDetailLoad } from '~/utils/discussionDetailLoad'
import { parseAnchorFromRouteQuery } from '~/utils/discussionAnchorQuery'
import {
  resolveTargetCommentId,
  scrollToCommentElement,
} from '~/utils/discussionCommentHash'
import {
  canResolveDiscussionSubThread,
  parseRepositoryRole,
  resolveEffectiveRepositoryRole,
  RepositoryRole,
  type RepositoryRoleValue,
} from '~/utils/discussionPermissions'

/** Shared state for the discussion detail page. */
export function useDiscussionDetailPage() {
  const route = useRoute()
  const auth = useAuth()
  const { t } = useI18n()
  const api = useApi()
  const { owner, repoSlug, repo, loading, notFound, loadRepo } = useRepoMetadata()

  const discussionNumber = computed(() => Number(route.params.number))
  const discussionRouteKey = computed(
    () => `${owner.value}/${repoSlug.value}/${discussionNumber.value}`,
  )

  const ctx = reactive({
    owner: '',
    repoSlug: '',
    loading: true,
    notFound: false,
    isWriterPlus: false,
    isClosed: false,
    discussion: null as Discussion | null,
    comments: [] as DiscussionComment[],
    commentsLoading: false,
    commentsError: null as string | null,
    members: [] as RepositoryMember[],
    viewerEffectiveRole: RepositoryRole.None as RepositoryRoleValue,
    pageLoading: false,
    forbidden: false,
    commentBody: '',
    posting: false,
    postError: null as string | null,
    commentAnchor: null as CommentAnchorInput | null,
    showAttachModal: false,
    resolving: false,
    dismissing: false,
    linkedMergeRequests: [] as DiscussionLinkedMergeRequest[],
    linkedDiscussions: [] as DiscussionDiscussionLink[],
  })

  const effectiveRepositoryRole = computed(() =>
    resolveEffectiveRepositoryRole({
      viewerEffectiveRole: ctx.viewerEffectiveRole,
      username: auth.user?.username,
      members: ctx.members,
      repo: repo.value,
    }),
  )

  const isWriterPlus = computed(() => effectiveRepositoryRole.value >= RepositoryRole.Writer)
  const isClosed = computed(() =>
    ctx.discussion?.status === 'Resolved' || ctx.discussion?.status === 'Dismissed',
  )

  watchEffect(() => {
    ctx.owner = owner.value
    ctx.repoSlug = repoSlug.value
    ctx.loading = loading.value
    ctx.notFound = notFound.value
    ctx.isWriterPlus = isWriterPlus.value
    ctx.isClosed = isClosed.value
  })

  useHead({
    title: computed(() =>
      ctx.discussion
        ? `#${ctx.discussion.number} ${ctx.discussion.title}`
        : t('repo.discussions.detailTitle'),
    ),
  })

  function statusColor(status: DiscussionStatus): 'success' | 'warning' | 'neutral' | 'info' {
    switch (status) {
      case 'Open':
        return 'success'
      case 'Engaged':
        return 'info'
      case 'Resolved':
        return 'neutral'
      case 'Dismissed':
        return 'warning'
    }
  }

  function statusLabel(status: DiscussionStatus): string {
    return t(`repo.discussions.status.${status.toLowerCase()}`)
  }

  function memberLabel(userId: string, preferredUsername?: string | null): string {
    if (preferredUsername) {
      return preferredUsername
    }
    const member = ctx.members.find(m => m.userId === userId)
    return member?.username ?? userId.slice(0, 8)
  }

  function memberInitial(userId: string): string {
    return memberLabel(userId).slice(0, 1).toUpperCase()
  }

  async function loadCommentsFallback(): Promise<void> {
    ctx.commentsLoading = true
    ctx.commentsError = null

    const result = await resolveCommentsFallbackLoad({
      listComments: () => api.discussions.listComments(
        owner.value,
        repoSlug.value,
        discussionNumber.value,
      ),
      commentsLoadErrorMessage: t('repo.discussions.commentsLoadError'),
    })
    ctx.comments = result.comments
    ctx.commentsError = result.commentsError
  }

  async function loadDiscussion(): Promise<void> {
    ctx.pageLoading = true
    ctx.forbidden = false
    ctx.commentsError = null

    try {
      const result = await resolveDiscussionDetailLoad({
        getResult: await api.discussions.get(
          owner.value,
          repoSlug.value,
          discussionNumber.value,
          { includeComments: true },
        ),
        listComments: () => api.discussions.listComments(
          owner.value,
          repoSlug.value,
          discussionNumber.value,
        ),
        commentsLoadErrorMessage: t('repo.discussions.commentsLoadError'),
      })

      ctx.forbidden = result.forbidden
      ctx.discussion = result.discussion
      ctx.comments = result.comments
      ctx.commentsError = result.commentsError
      ctx.viewerEffectiveRole = parseRepositoryRole(result.discussion?.viewerEffectiveRole)
      await Promise.all([loadLinkedMergeRequests(), loadLinkedDiscussions()])
    }
    finally {
      ctx.pageLoading = false
      ctx.commentsLoading = false
      queueCommentHashScroll()
    }
  }

  async function loadLinkedMergeRequests(): Promise<void> {
    const result = await api.discussions.listLinkedMergeRequests(
      owner.value,
      repoSlug.value,
      discussionNumber.value,
    )
    ctx.linkedMergeRequests = result.data ?? []
  }

  async function loadLinkedDiscussions(): Promise<void> {
    const result = await api.discussions.listDiscussionLinks(
      owner.value,
      repoSlug.value,
      discussionNumber.value,
    )
    ctx.linkedDiscussions = result.data ?? []
  }

  async function addDiscussionLink(
    targetDiscussionNumber: number,
    relationshipType: DiscussionLinkType,
  ): Promise<boolean> {
    const result = await api.discussions.createDiscussionLink(
      owner.value,
      repoSlug.value,
      discussionNumber.value,
      { targetDiscussionNumber, relationshipType },
    )
    if (result.status !== 200 || !result.data) {
      return false
    }

    await loadLinkedDiscussions()
    return true
  }

  async function removeDiscussionLink(link: DiscussionDiscussionLink): Promise<void> {
    const result = await api.discussions.deleteDiscussionLink(
      owner.value,
      repoSlug.value,
      discussionNumber.value,
      link.targetDiscussionNumber,
      link.relationshipType,
    )
    if (result.status === 204 || result.status === 200) {
      await loadLinkedDiscussions()
    }
  }

  function queueCommentHashScroll(): void {
    if (!import.meta.client) {
      return
    }
    const commentId = resolveTargetCommentId({ hash: route.hash, query: route.query })
    if (!commentId) {
      return
    }
    nextTick(() => {
      scrollToCommentElement(commentId)
    })
  }

  async function retryLoadComments(): Promise<void> {
    ctx.commentsLoading = true
    ctx.commentsError = null
    try {
      await loadCommentsFallback()
    }
    finally {
      ctx.commentsLoading = false
    }
  }

  async function initializePage(): Promise<void> {
    await loadRepo()
    if (!notFound.value) {
      await Promise.all([loadDiscussion(), loadMembers()])
    }
    const draftAnchor = parseAnchorFromRouteQuery(route.query)
    if (draftAnchor) {
      ctx.commentAnchor = draftAnchor
    }
  }

  onMounted(() => {
    void initializePage()
  })

  watch(discussionRouteKey, (next, prev) => {
    if (!prev || next === prev) {
      return
    }
    void initializePage()
  })

  watch(
    () => [
      resolveTargetCommentId({ hash: route.hash, query: route.query }),
      ctx.comments.length,
      ctx.pageLoading,
      ctx.loading,
      ctx.commentsLoading,
    ] as const,
    ([commentId, , pageLoading, loading, commentsLoading]) => {
      if (!commentId || pageLoading || loading || commentsLoading) {
        return
      }
      nextTick(() => {
        scrollToCommentElement(commentId)
      })
    },
    { flush: 'post' },
  )

  watch(
    () => [route.hash, route.query.comment] as const,
    async ([hash, commentQuery], oldValue) => {
      if (!oldValue) {
        return
      }
      const [oldHash, oldCommentQuery] = oldValue
      const targetChanged = hash !== oldHash || commentQuery !== oldCommentQuery
      const commentId = resolveTargetCommentId({ hash, query: route.query })
      if (!commentId || !targetChanged || ctx.pageLoading) {
        return
      }
      await loadDiscussion()
    },
  )

  async function loadMembers(): Promise<void> {
    if (!repo.value?.id) {
      return
    }
    const result = await api.repositoryMembers.list(repo.value.id)
    ctx.members = result.data ?? []
  }

  async function postComment(): Promise<void> {
    if (!auth.isAuthenticated) {
      await navigateTo('/sign-in')
      return
    }
    ctx.posting = true
    ctx.postError = null
    try {
      const result = await api.discussions.createComment(
        owner.value,
        repoSlug.value,
        discussionNumber.value,
        {
          bodyMarkdown: ctx.commentBody.trim(),
          anchor: ctx.commentAnchor,
        },
      )
      if (result.error) {
        ctx.postError = result.error
        return
      }
      ctx.commentBody = ''
      ctx.commentAnchor = null
      await loadDiscussion()
    }
    finally {
      ctx.posting = false
    }
  }

  async function postReply(
    parentCommentId: string,
    bodyMarkdown: string,
    anchor: CommentAnchorInput | null,
  ): Promise<void> {
    if (!auth.isAuthenticated) {
      await navigateTo('/sign-in')
      return
    }
    const result = await api.discussions.createComment(
      owner.value,
      repoSlug.value,
      discussionNumber.value,
      {
        bodyMarkdown,
        parentCommentId,
        anchor,
      },
    )
    if (result.error) {
      ctx.postError = result.error
      return
    }
    await loadDiscussion()
  }

  function canResolveSubThread(comment: DiscussionComment): boolean {
    return canResolveDiscussionSubThread({
      comment,
      userId: auth.user?.userId,
      username: auth.user?.username,
      effectiveRole: effectiveRepositoryRole.value,
    })
  }

  async function resolveSubThread(commentId: string): Promise<void> {
    await api.discussions.resolveSubThread(owner.value, repoSlug.value, commentId)
    await loadDiscussion()
  }

  async function unresolveSubThread(commentId: string): Promise<void> {
    await api.discussions.unresolveSubThread(owner.value, repoSlug.value, commentId)
    await loadDiscussion()
  }

  async function resolveDiscussion(): Promise<void> {
    ctx.resolving = true
    try {
      await api.discussions.resolve(owner.value, repoSlug.value, discussionNumber.value)
      await loadDiscussion()
    }
    finally {
      ctx.resolving = false
    }
  }

  async function dismissDiscussion(): Promise<void> {
    ctx.dismissing = true
    try {
      await api.discussions.dismiss(owner.value, repoSlug.value, discussionNumber.value)
      await loadDiscussion()
    }
    finally {
      ctx.dismissing = false
    }
  }

  return Object.assign(ctx, {
    auth,
    t,
    statusColor,
    statusLabel,
    memberLabel,
    memberInitial,
    postComment,
    postReply,
    canResolveSubThread,
    resolveSubThread,
    unresolveSubThread,
    resolveDiscussion,
    dismissDiscussion,
    retryLoadComments,
    addDiscussionLink,
    removeDiscussionLink,
  })
}

export type DiscussionDetailPageContext = ReturnType<typeof useDiscussionDetailPage>
