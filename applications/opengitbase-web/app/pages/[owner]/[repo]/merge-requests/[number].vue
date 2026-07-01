<script setup lang="ts">
import type {
  DiffSide,
  MergeRequest,
  MergeRequestChanges,
  MergeRequestComment,
  MergeRequestCommentAnchor,
  MergeRequestCommit,
  MergeRequestDiscussionLink,
  MergeRequestLinkType,
  RepositoryMember,
} from '~/utils/api'
import type { CollaborationThread } from '~/components/collaboration/types'

const route = useRoute()
const { t } = useI18n()
const auth = useAuth()
const api = useApi()

const owner = computed(() => String(route.params.owner))
const repoSlug = computed(() => String(route.params.repo))
const number = computed(() => Number(route.params.number))

const mr = ref<MergeRequest | null>(null)
const changes = ref<MergeRequestChanges | null>(null)
const commits = ref<MergeRequestCommit[]>([])
const overviewComments = ref<MergeRequestComment[]>([])
const reviewComments = ref<MergeRequestComment[]>([])
const linkedDiscussions = ref<MergeRequestDiscussionLink[]>([])
const members = ref<RepositoryMember[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const activeTab = ref<'overview' | 'changes' | 'commits'>('overview')

const commentBody = ref('')
const replyError = ref<string | null>(null)
const selectedLine = ref<MergeRequestCommentAnchor | null>(null)
const lineCommentBody = ref('')

function statusColor(status: MergeRequest['status']): 'neutral' | 'info' | 'success' | 'warning' {
  switch (status) {
    case 'Draft':
      return 'neutral'
    case 'Open':
      return 'info'
    case 'Approved':
      return 'success'
    case 'Merged':
      return 'success'
    case 'Closed':
      return 'warning'
  }
}

function memberLabel(_userId: string, preferred?: string | null): string {
  return preferred ?? 'unknown'
}

function canEditComment(comment: MergeRequestComment): boolean {
  return auth.isAuthenticated && auth.user?.userId === comment.authorUserId
}

function canDeleteComment(comment: MergeRequestComment): boolean {
  if (!auth.isAuthenticated) {
    return false
  }
  if (auth.user?.userId === comment.authorUserId) {
    return true
  }
  const member = members.value.find(item => item.userId === auth.user?.userId)
  return (member?.role ?? 0) >= 2
}

const tabItems = computed(() => ([
  { label: t('repo.mergeRequests.tabs.overview'), value: 'overview' },
  { label: t('repo.mergeRequests.tabs.changes'), value: 'changes' },
  { label: t('repo.mergeRequests.tabs.commits'), value: 'commits' },
]))

function lineKey(anchor: MergeRequestCommentAnchor | null | undefined): string {
  if (!anchor) {
    return ''
  }
  return `${anchor.filePath}:${anchor.lineNumber}:${anchor.diffSide}`
}

function asThread(comment: MergeRequestComment): CollaborationThread {
  const mapReply = (reply: MergeRequestComment) => ({
    id: reply.id,
    author: {
      userId: reply.authorUserId,
      username: reply.authorUsername,
    },
    bodyMarkdown: reply.bodyMarkdown,
    createdAt: reply.createdAt,
    anchor: reply.anchor ? {
      ref: reply.anchor.headCommitSha,
      commitSha: reply.anchor.headCommitSha,
      filePath: reply.anchor.filePath,
      line: reply.anchor.lineNumber,
    } : null,
  })
  return {
    id: comment.id,
    author: {
      userId: comment.authorUserId,
      username: comment.authorUsername,
    },
    bodyMarkdown: comment.bodyMarkdown,
    createdAt: comment.createdAt,
    isResolved: comment.isResolved,
    isOutdated: comment.isOutdated,
    replyCount: comment.replyCount,
    replies: comment.replies.map(mapReply),
    anchor: comment.anchor ? {
      ref: comment.anchor.headCommitSha,
      commitSha: comment.anchor.headCommitSha,
      filePath: comment.anchor.filePath,
      line: comment.anchor.lineNumber,
    } : null,
  }
}

async function loadAll(): Promise<void> {
  loading.value = true
  error.value = null
  const [mrResult, overviewResult, reviewResult, changesResult, commitsResult, linksResult] = await Promise.all([
    api.mergeRequests.get(owner.value, repoSlug.value, number.value),
    api.mergeRequests.listComments(owner.value, repoSlug.value, number.value, { type: 'overview' }),
    api.mergeRequests.listComments(owner.value, repoSlug.value, number.value, { type: 'review' }),
    api.mergeRequests.getChanges(owner.value, repoSlug.value, number.value),
    api.mergeRequests.listCommits(owner.value, repoSlug.value, number.value),
    api.mergeRequests.listDiscussionLinks(owner.value, repoSlug.value, number.value),
  ])
  if (mrResult.error || !mrResult.data) {
    error.value = mrResult.error ?? t('repo.mergeRequests.notFound')
    loading.value = false
    return
  }
  mr.value = mrResult.data
  overviewComments.value = overviewResult.data ?? []
  reviewComments.value = reviewResult.data ?? []
  changes.value = changesResult.data
  commits.value = commitsResult.data ?? []
  linkedDiscussions.value = linksResult.data ?? []
  if (mr.value) {
    const membersResult = await api.repositoryMembers.list(mr.value.repositoryId)
    members.value = membersResult.data ?? []
  }
  loading.value = false
}

function reviewThreadsForLine(filePath: string, lineNumber: number, diffSide: DiffSide): MergeRequestComment[] {
  return reviewComments.value.filter((comment) => {
    return comment.anchor?.filePath === filePath
      && comment.anchor.lineNumber === lineNumber
      && comment.anchor.diffSide === diffSide
      && !comment.parentCommentId
  })
}

async function postOverviewComment(): Promise<void> {
  if (!commentBody.value.trim()) {
    return
  }
  const result = await api.mergeRequests.createComment(owner.value, repoSlug.value, number.value, {
    bodyMarkdown: commentBody.value.trim(),
  })
  if (result.error) {
    replyError.value = result.error
    return
  }
  commentBody.value = ''
  await loadAll()
}

async function postReviewComment(): Promise<void> {
  if (!selectedLine.value || !lineCommentBody.value.trim()) {
    return
  }
  const result = await api.mergeRequests.createComment(owner.value, repoSlug.value, number.value, {
    bodyMarkdown: lineCommentBody.value.trim(),
    anchor: selectedLine.value,
  })
  if (result.error) {
    replyError.value = result.error
    return
  }
  lineCommentBody.value = ''
  selectedLine.value = null
  await loadAll()
}

async function replyToComment(parentCommentId: string, body: string, anchor: MergeRequestCommentAnchor | null): Promise<void> {
  const result = await api.mergeRequests.createComment(owner.value, repoSlug.value, number.value, {
    bodyMarkdown: body,
    parentCommentId,
    anchor,
  })
  if (result.error) {
    replyError.value = result.error
    return
  }
  await loadAll()
}

async function resolveComment(commentId: string): Promise<void> {
  await api.mergeRequests.resolveComment(owner.value, repoSlug.value, number.value, commentId)
  await loadAll()
}

async function unresolveComment(commentId: string): Promise<void> {
  await api.mergeRequests.unresolveComment(owner.value, repoSlug.value, number.value, commentId)
  await loadAll()
}

async function addDiscussionLink(discussionNumber: number, relationshipType: MergeRequestLinkType): Promise<boolean> {
  const result = await api.mergeRequests.createDiscussionLink(owner.value, repoSlug.value, number.value, {
    discussionNumber,
    relationshipType,
  })
  if (result.error) {
    replyError.value = result.error
    return false
  }
  await loadAll()
  return true
}

async function removeDiscussionLink(link: MergeRequestDiscussionLink): Promise<void> {
  await api.mergeRequests.deleteDiscussionLink(
    owner.value,
    repoSlug.value,
    number.value,
    link.discussionNumber,
    link.relationshipType,
  )
  await loadAll()
}

async function editOverviewComment(commentId: string, bodyMarkdown: string): Promise<void> {
  const result = await api.mergeRequests.updateComment(owner.value, repoSlug.value, number.value, commentId, {
    bodyMarkdown,
  })
  if (result.error) {
    replyError.value = result.error
    return
  }
  await loadAll()
}

async function deleteOverviewComment(commentId: string): Promise<void> {
  const result = await api.mergeRequests.deleteComment(owner.value, repoSlug.value, number.value, commentId)
  if (result.error) {
    replyError.value = result.error
    return
  }
  await loadAll()
}

onMounted(() => {
  void loadAll()
})
</script>

<template>
  <div class="mx-auto max-w-7xl space-y-4">
    <UButton
      :to="`/${owner}/${repoSlug}/merge-requests`"
      variant="ghost"
      icon="i-lucide-arrow-left"
      size="sm"
    >
      {{ t('repo.mergeRequests.title') }}
    </UButton>

    <UCard v-if="loading || error || !mr">
      <p
        v-if="loading"
        class="text-sm text-[var(--ogb-text-muted)]"
      >
        {{ t('common.loading') }}
      </p>
      <p
        v-else
        class="text-sm text-[var(--ogb-text-muted)]"
      >
        {{ error ?? t('repo.mergeRequests.notFound') }}
      </p>
    </UCard>

    <template v-else>
      <header class="space-y-2">
        <p class="font-mono text-sm text-[var(--ogb-text-muted)]">
          !{{ mr.number }} · {{ owner }}/{{ repoSlug }}
        </p>
        <h1 class="text-2xl font-semibold">
          {{ mr.title }}
        </h1>
        <div class="flex flex-wrap items-center gap-2">
          <CollaborationStatusBadge
            :label="mr.status"
            :color="statusColor(mr.status)"
          />
          <span class="text-xs text-[var(--ogb-text-muted)]">{{ mr.sourceRef }} → {{ mr.targetRef }}</span>
        </div>
      </header>

      <div class="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_280px]">
        <main class="space-y-4">
          <UTabs
            v-model="activeTab"
            :items="tabItems"
            value-key="value"
            label-key="label"
          />

          <section v-if="activeTab === 'overview'" class="space-y-4">
            <UCard v-if="mr.body">
              <CollaborationRenderedBody :source="mr.body" />
            </UCard>
            <UCard>
              <template #header>
                <h2 class="font-semibold">
                  {{ t('repo.mergeRequests.overviewComments') }}
                </h2>
              </template>
              <div class="space-y-3">
                <p
                  v-if="!overviewComments.length"
                  class="text-sm text-[var(--ogb-text-muted)]"
                >
                  {{ t('repo.mergeRequests.noComments') }}
                </p>
                <CollaborationFlatComment
                  v-for="comment in overviewComments.filter(c => !c.parentCommentId && !c.isDeleted)"
                  :key="comment.id"
                  :id="comment.id"
                  :author="{ userId: comment.authorUserId, username: comment.authorUsername }"
                  :body-markdown="comment.bodyMarkdown"
                  :created-at="comment.createdAt"
                  :edited-at="comment.editedAt"
                  :member-label="memberLabel"
                  :can-edit="canEditComment(comment)"
                  :can-delete="canDeleteComment(comment)"
                  :edited-label="t('repo.mergeRequests.editedComment')"
                  @edit="body => editOverviewComment(comment.id, body)"
                  @delete="deleteOverviewComment(comment.id)"
                />

                <div
                  v-if="auth.isAuthenticated"
                  class="space-y-2 border-t pt-3"
                  style="border-color: var(--ogb-border);"
                >
                  <CollaborationMarkdownEditor
                    v-model="commentBody"
                    :members="members"
                    :placeholder="t('repo.mergeRequests.commentPlaceholder')"
                    min-height="5rem"
                  />
                  <UButton
                    :disabled="!commentBody.trim()"
                    @click="postOverviewComment"
                  >
                    {{ t('repo.mergeRequests.postComment') }}
                  </UButton>
                </div>
                <p
                  v-else
                  class="text-sm text-[var(--ogb-text-muted)]"
                >
                  {{ t('repo.discussions.signInToComment') }}
                </p>
              </div>
            </UCard>
          </section>

          <section v-else-if="activeTab === 'changes'" class="space-y-4">
            <UCard v-if="!changes?.files?.length">
              <p class="text-sm text-[var(--ogb-text-muted)]">
                {{ t('repo.mergeRequests.noChanges') }}
              </p>
            </UCard>
            <UCard
              v-for="file in changes?.files ?? []"
              :key="file.filePath"
              class="overflow-hidden"
            >
              <template #header>
                <div class="flex items-center justify-between gap-3">
                  <p class="font-mono text-sm">
                    {{ file.filePath }}
                  </p>
                  <UBadge
                    variant="subtle"
                    color="neutral"
                  >
                    {{ file.changeType }}
                  </UBadge>
                </div>
              </template>
              <div class="space-y-4">
                <div
                  v-for="hunk in file.hunks"
                  :key="hunk.header"
                  class="overflow-hidden rounded border"
                  style="border-color: var(--ogb-border);"
                >
                  <p class="border-b px-3 py-2 font-mono text-xs text-[var(--ogb-text-muted)]" style="border-color: var(--ogb-border);">
                    {{ hunk.header }}
                  </p>
                  <div class="font-mono text-xs">
                    <div
                      v-for="(line, idx) in hunk.lines"
                      :key="`${hunk.header}-${idx}`"
                      class="border-b px-3 py-1"
                      style="border-color: color-mix(in srgb, var(--ogb-border) 50%, transparent);"
                      :class="{
                        'bg-emerald-500/10': line.type === 'add',
                        'bg-rose-500/10': line.type === 'remove',
                      }"
                    >
                      <div class="flex items-start gap-2">
                        <button
                          v-if="line.newLineNumber || line.oldLineNumber"
                          type="button"
                          class="text-[10px] text-[var(--ogb-text-muted)] hover:text-[var(--ogb-text)]"
                          @click="selectedLine = {
                            headCommitSha: mr.sourceHeadSha,
                            filePath: file.filePath,
                            lineNumber: line.newLineNumber ?? line.oldLineNumber ?? 0,
                            diffSide: line.type === 'remove' ? 'old' : 'new',
                          }"
                        >
                          +
                        </button>
                        <span class="w-8 text-right text-[var(--ogb-text-muted)]">{{ line.oldLineNumber ?? '' }}</span>
                        <span class="w-8 text-right text-[var(--ogb-text-muted)]">{{ line.newLineNumber ?? '' }}</span>
                        <span class="min-w-0 flex-1 whitespace-pre-wrap">{{ line.content }}</span>
                      </div>

                      <div
                        v-if="reviewThreadsForLine(file.filePath, line.newLineNumber ?? line.oldLineNumber ?? -1, line.type === 'remove' ? 'old' : 'new').length"
                        class="mt-2 space-y-2 border-l pl-2"
                        style="border-color: var(--ogb-border);"
                      >
                        <CollaborationThread
                          v-for="comment in reviewThreadsForLine(file.filePath, line.newLineNumber ?? line.oldLineNumber ?? -1, line.type === 'remove' ? 'old' : 'new')"
                          :key="comment.id"
                          :thread="asThread(comment)"
                          :owner="owner"
                          :repo-slug="repoSlug"
                          :member-label="memberLabel"
                          :members="members"
                          :can-resolve="auth.isAuthenticated"
                          :can-reply="auth.isAuthenticated"
                          :resolved-label="t('repo.discussions.subThreadResolved')"
                          :outdated-label="t('repo.mergeRequests.outdated')"
                          :reply-count-label="(count: number) => t('repo.discussions.replyCount', { count })"
                          @reply="(body, anchor) => replyToComment(comment.id, body, anchor ? {
                            headCommitSha: anchor.commitSha,
                            filePath: anchor.filePath,
                            lineNumber: anchor.line,
                            diffSide: 'new',
                          } : null)"
                          @resolve="resolveComment(comment.id)"
                          @unresolve="unresolveComment(comment.id)"
                        />
                      </div>
                    </div>
                  </div>
                </div>
              </div>
            </UCard>

            <UCard v-if="selectedLine">
              <template #header>
                <h3 class="font-semibold">
                  {{ t('repo.mergeRequests.addReviewComment') }}
                </h3>
              </template>
              <p class="mb-2 text-xs text-[var(--ogb-text-muted)]">
                {{ selectedLine.filePath }}:{{ selectedLine.lineNumber }} ({{ selectedLine.diffSide }})
              </p>
              <CollaborationMarkdownEditor
                v-model="lineCommentBody"
                :placeholder="t('repo.mergeRequests.commentPlaceholder')"
                min-height="5rem"
              />
              <div class="mt-3 flex gap-2">
                <UButton
                  :disabled="!lineCommentBody.trim()"
                  @click="postReviewComment"
                >
                  {{ t('repo.mergeRequests.postComment') }}
                </UButton>
                <UButton
                  variant="ghost"
                  @click="selectedLine = null"
                >
                  {{ t('common.cancel') }}
                </UButton>
              </div>
            </UCard>
          </section>

          <section v-else class="space-y-2">
            <UCard v-if="!commits.length">
              <p class="text-sm text-[var(--ogb-text-muted)]">
                {{ t('repo.mergeRequests.noCommits') }}
              </p>
            </UCard>
            <UCard
              v-for="commit in commits"
              :key="commit.sha"
            >
              <p class="font-medium">
                {{ commit.message }}
              </p>
              <p class="mt-1 font-mono text-xs text-[var(--ogb-text-muted)]">
                {{ commit.shortSha }} · {{ commit.authorName }} · <RelativeTime :iso="commit.authoredAt" />
              </p>
            </UCard>
          </section>
        </main>

        <aside class="space-y-4">
          <MergeRequestLinkedDiscussions
            :owner="owner"
            :repo-slug="repoSlug"
            :linked-discussions="linkedDiscussions"
            :add-link="addDiscussionLink"
            :remove-link="removeDiscussionLink"
          />
        </aside>
      </div>

      <UAlert
        v-if="replyError"
        color="error"
        variant="subtle"
        :description="replyError"
      />
    </template>
  </div>
</template>
