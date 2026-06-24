import type {
  CommentAnchorInput,
  Discussion,
  DiscussionStatus,
  RepositoryMember,
  RepositoryTag,
} from '~/utils/api'
import { parseAnchorFromRouteQuery } from '~/utils/discussionAnchorQuery'

/** Shared state for the discussions list page. */
export function useDiscussionsListPage() {
  const auth = useAuth()
  const { t } = useI18n()
  const api = useApi()
  const { owner, repoSlug, repo, loading, notFound, loadRepo } = useRepoMetadata()

  const ctx = reactive({
    owner: '',
    repoSlug: '',
    repo: null as ReturnType<typeof useRepoMetadata>['repo']['value'],
    loading: true,
    notFound: false,
    discussions: [] as Discussion[],
    tags: [] as RepositoryTag[],
    listLoading: false,
    forbidden: false,
    signInRequired: false,
    showCreate: false,
    createTitle: '',
    createBody: '',
    createTagIds: [] as string[],
    creating: false,
    createError: null as string | null,
    pendingAnchor: null as CommentAnchorInput | null,
    showAttachModal: false,
    members: [] as RepositoryMember[],
    statusFilter: 'all' as DiscussionStatus | 'all',
    tagFilter: 'all' as string | 'all',
    statusOptions: [] as Array<{ label: string, value: string }>,
    tagOptions: [] as Array<{ label: string, value: string }>,
    suggestedTitle: '',
  })

  const statusOptions = computed(() => [
    { label: t('repo.discussions.filters.allStatuses'), value: 'all' },
    { label: t('repo.discussions.status.open'), value: 'Open' as const },
    { label: t('repo.discussions.status.engaged'), value: 'Engaged' as const },
    { label: t('repo.discussions.status.resolved'), value: 'Resolved' as const },
    { label: t('repo.discussions.status.dismissed'), value: 'Dismissed' as const },
  ])

  const tagOptions = computed(() => [
    { label: t('repo.discussions.filters.allTags'), value: 'all' },
    ...ctx.tags.map(tag => ({ label: tag.name, value: tag.id })),
  ])

  const suggestedTitle = computed(() =>
    ctx.pendingAnchor
      ? `Note on \`${ctx.pendingAnchor.filePath}:${ctx.pendingAnchor.line}\``
      : '',
  )

  watchEffect(() => {
    ctx.owner = owner.value
    ctx.repoSlug = repoSlug.value
    ctx.repo = repo.value
    ctx.loading = loading.value
    ctx.notFound = notFound.value
    ctx.statusOptions = statusOptions.value
    ctx.tagOptions = tagOptions.value
    ctx.suggestedTitle = suggestedTitle.value
  })

  useHead({
    title: computed(() => `${t('repo.discussions.heading')} · ${owner.value}/${repoSlug.value}`),
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

  async function loadDiscussions(): Promise<void> {
    ctx.listLoading = true
    ctx.forbidden = false
    ctx.signInRequired = false

    const params: { status?: DiscussionStatus, tagId?: string } = {}
    if (ctx.statusFilter !== 'all') {
      params.status = ctx.statusFilter
    }
    if (ctx.tagFilter !== 'all') {
      params.tagId = ctx.tagFilter
    }

    const result = await api.discussions.list(owner.value, repoSlug.value, params)
    if (result.status === 403) {
      ctx.forbidden = true
    }
    else if (result.status === 401) {
      ctx.signInRequired = true
    }
    else {
      ctx.discussions = result.data ?? []
    }
    ctx.listLoading = false
  }

  async function loadTags(): Promise<void> {
    const result = await api.discussions.tags.list(owner.value, repoSlug.value)
    ctx.tags = result.data ?? []
  }

  async function loadMembers(): Promise<void> {
    if (!repo.value?.id) {
      return
    }
    const result = await api.repositoryMembers.list(repo.value.id)
    ctx.members = result.data ?? []
  }

  async function createDiscussion(): Promise<void> {
    if (!auth.isAuthenticated) {
      await navigateTo('/sign-in')
      return
    }
    ctx.creating = true
    ctx.createError = null
    try {
      const result = await api.discussions.create(owner.value, repoSlug.value, {
        title: ctx.createTitle.trim(),
        body: ctx.createBody.trim() || null,
        tagIds: ctx.createTagIds,
        anchor: ctx.pendingAnchor,
      })
      if (result.error) {
        ctx.createError = result.error
        return
      }
      if (result.data) {
        await navigateTo(`/${owner.value}/${repoSlug.value}/discussions/${result.data.number}`)
      }
    }
    finally {
      ctx.creating = false
    }
  }

  function openCreate(): void {
    if (!auth.isAuthenticated) {
      void navigateTo('/sign-in')
      return
    }
    ctx.showCreate = true
  }

  function setPendingAnchor(anchor: CommentAnchorInput | null): void {
    ctx.pendingAnchor = anchor
    if (anchor && !ctx.createTitle) {
      ctx.createTitle = `Note on \`${anchor.filePath}:${anchor.line}\``
    }
  }

  watch(
    () => [ctx.statusFilter, ctx.tagFilter] as const,
    () => {
      void loadDiscussions()
    },
  )

  onMounted(async () => {
    await loadRepo()
    if (!notFound.value) {
      await Promise.all([loadDiscussions(), loadTags(), loadMembers()])
    }
    applyRouteAnchorDraft()
  })

  const route = useRoute()

  function applyRouteAnchorDraft(): void {
    const draftAnchor = parseAnchorFromRouteQuery(route.query)
    if (draftAnchor) {
      setPendingAnchor(draftAnchor)
    }
    if (route.query.openCreate === '1') {
      openCreate()
    }
  }

  watch(
    () => route.query,
    () => {
      applyRouteAnchorDraft()
    },
  )

  return Object.assign(ctx, {
    auth,
    t,
    statusColor,
    statusLabel,
    loadDiscussions,
    createDiscussion,
    openCreate,
    setPendingAnchor,
  })
}

export type DiscussionsListPageContext = ReturnType<typeof useDiscussionsListPage>
