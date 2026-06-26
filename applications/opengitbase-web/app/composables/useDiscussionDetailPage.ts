import type {
  CommentAnchorInput,
  Discussion,
  DiscussionComment,
  DiscussionStatus,
  RepositoryMember,
} from '~/utils/api'
import { resolveCommentsFallbackLoad, resolveDiscussionDetailLoad } from '~/utils/discussionDetailLoad'
import { parseAnchorFromRouteQuery } from '~/utils/discussionAnchorQuery'

/** Shared state for the discussion detail page. */
export function useDiscussionDetailPage() {
  const route = useRoute()
  const auth = useAuth()
  const { t } = useI18n()
  const api = useApi()
  const { owner, repoSlug, repo, loading, notFound, loadRepo } = useRepoMetadata()

  const discussionNumber = computed(() => Number(route.params.number))

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
    pageLoading: false,
    forbidden: false,
    commentBody: '',
    posting: false,
    postError: null as string | null,
    commentAnchor: null as CommentAnchorInput | null,
    showAttachModal: false,
    resolving: false,
    dismissing: false,
  })

  const currentMemberRole = computed(() => {
    if (!auth.user) {
      return 0
    }
    const member = ctx.members.find(m => m.username === auth.user?.username)
    return member?.role ?? 0
  })

  const isWriterPlus = computed(() => currentMemberRole.value >= 2)
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
    }
    finally {
      ctx.pageLoading = false
      ctx.commentsLoading = false
      scrollToCommentFromHash()
    }
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

  function scrollToCommentFromHash(): void {
    if (!import.meta.client) {
      return
    }
    const hash = route.hash
    if (!hash.startsWith('#comment-')) {
      return
    }
    nextTick(() => {
      nextTick(() => {
        document.querySelector(hash)?.scrollIntoView({ behavior: 'smooth', block: 'center' })
      })
    })
  }

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
    if (!auth.user) {
      return false
    }
    const member = ctx.members.find(m => m.username === auth.user?.username)
    const isAuthor = member?.userId === comment.authorUserId
    return isAuthor || isWriterPlus.value
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

  onMounted(async () => {
    await loadRepo()
    if (!notFound.value) {
      await Promise.all([loadDiscussion(), loadMembers()])
    }
    const draftAnchor = parseAnchorFromRouteQuery(route.query)
    if (draftAnchor) {
      ctx.commentAnchor = draftAnchor
    }
  })

  watch(() => route.hash, () => {
    if (!ctx.pageLoading) {
      scrollToCommentFromHash()
    }
  })

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
  })
}

export type DiscussionDetailPageContext = ReturnType<typeof useDiscussionDetailPage>
