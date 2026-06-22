<script setup lang="ts">
import type {
  Discussion,
  DiscussionComment,
  DiscussionStatus,
  RepositoryMember,
} from '~/utils/api'

const route = useRoute()
const auth = useAuth()
const { t } = useI18n()
const api = useApi()
const { owner, repoSlug, repo, loading, notFound, loadRepo } = useRepoMetadata()

const discussionNumber = computed(() => Number(route.params.number))

const discussion = ref<Discussion | null>(null)
const comments = ref<DiscussionComment[]>([])
const members = ref<RepositoryMember[]>([])
const pageLoading = ref(false)
const forbidden = ref(false)

const commentBody = ref('')
const posting = ref(false)
const postError = ref<string | null>(null)
const resolving = ref(false)
const dismissing = ref(false)

const currentMemberRole = computed(() => {
  if (!auth.user) {
    return 0
  }
  const member = members.value.find(m => m.username === auth.user?.username)
  return member?.role ?? 0
})

const isWriterPlus = computed(() => currentMemberRole.value >= 2)
const isClosed = computed(() =>
  discussion.value?.status === 'Resolved' || discussion.value?.status === 'Dismissed',
)

useHead({
  title: computed(() =>
    discussion.value
      ? `#${discussion.value.number} ${discussion.value.title}`
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

function memberLabel(userId: string): string {
  const member = members.value.find(m => m.userId === userId)
  return member?.username ?? userId
}

async function loadDiscussion(): Promise<void> {
  pageLoading.value = true
  forbidden.value = false

  const result = await api.discussions.get(owner.value, repoSlug.value, discussionNumber.value)
  if (result.status === 403) {
    forbidden.value = true
    pageLoading.value = false
    return
  }
  if (result.status === 404) {
    discussion.value = null
    pageLoading.value = false
    return
  }
  discussion.value = result.data

  const commentsResult = await api.discussions.listComments(
    owner.value,
    repoSlug.value,
    discussionNumber.value,
  )
  comments.value = commentsResult.data ?? []
  pageLoading.value = false
}

async function loadMembers(): Promise<void> {
  if (!repo.value?.id) {
    return
  }
  const result = await api.repositoryMembers.list(repo.value.id)
  members.value = result.data ?? []
}

async function postComment(): Promise<void> {
  if (!auth.isAuthenticated) {
    await navigateTo('/sign-in')
    return
  }
  posting.value = true
  postError.value = null
  try {
    const result = await api.discussions.createComment(
      owner.value,
      repoSlug.value,
      discussionNumber.value,
      { bodyMarkdown: commentBody.value.trim() },
    )
    if (result.error) {
      postError.value = result.error
      return
    }
    commentBody.value = ''
    await loadDiscussion()
  }
  finally {
    posting.value = false
  }
}

async function resolveDiscussion(): Promise<void> {
  resolving.value = true
  try {
    await api.discussions.resolve(owner.value, repoSlug.value, discussionNumber.value)
    await loadDiscussion()
  }
  finally {
    resolving.value = false
  }
}

async function dismissDiscussion(): Promise<void> {
  dismissing.value = true
  try {
    await api.discussions.dismiss(owner.value, repoSlug.value, discussionNumber.value)
    await loadDiscussion()
  }
  finally {
    dismissing.value = false
  }
}

onMounted(async () => {
  await loadRepo()
  if (!notFound.value) {
    await Promise.all([loadDiscussion(), loadMembers()])
  }
})
</script>

<template>
  <div class="mx-auto max-w-3xl space-y-6">
    <UButton
      :to="`/${owner}/${repoSlug}/discussions`"
      variant="ghost"
      icon="i-lucide-arrow-left"
      size="sm"
    >
      {{ t('repo.discussions.backToList') }}
    </UButton>

    <div
      v-if="loading || pageLoading"
      class="text-sm text-[var(--ogb-text-muted)]"
    >
      {{ t('common.loading') }}
    </div>

    <UCard v-else-if="notFound || forbidden || !discussion">
      <p class="text-sm text-[var(--ogb-text-muted)]">
        {{ forbidden ? t('repo.browse.forbidden') : t('repo.discussions.notFound') }}
      </p>
    </UCard>

    <template v-else>
      <div class="space-y-3">
        <div class="flex flex-wrap items-start justify-between gap-4">
          <div class="min-w-0 space-y-2">
            <p class="font-mono text-sm text-[var(--ogb-text-muted)]">
              #{{ discussion.number }}
            </p>
            <h1 class="text-2xl font-semibold">
              {{ discussion.title }}
            </h1>
            <div class="flex flex-wrap items-center gap-2">
              <UBadge
                :color="statusColor(discussion.status)"
                variant="subtle"
              >
                {{ statusLabel(discussion.status) }}
              </UBadge>
              <UBadge
                v-for="tag in discussion.tags"
                :key="tag.id"
                color="neutral"
                variant="subtle"
                size="sm"
              >
                {{ tag.name }}
              </UBadge>
            </div>
          </div>
          <div
            v-if="isWriterPlus && !isClosed"
            class="flex flex-wrap gap-2"
          >
            <UButton
              color="success"
              variant="soft"
              size="sm"
              icon="i-lucide-check-circle"
              :loading="resolving"
              @click="resolveDiscussion"
            >
              {{ t('repo.discussions.resolve') }}
            </UButton>
            <UButton
              color="warning"
              variant="soft"
              size="sm"
              icon="i-lucide-x-circle"
              :loading="dismissing"
              @click="dismissDiscussion"
            >
              {{ t('repo.discussions.dismiss') }}
            </UButton>
          </div>
        </div>

        <p
          v-if="discussion.assigneeUserId"
          class="text-sm text-[var(--ogb-text-muted)]"
        >
          {{ t('repo.discussions.assignee') }}: {{ memberLabel(discussion.assigneeUserId) }}
        </p>

        <p class="text-xs text-[var(--ogb-text-muted)]">
          {{ t('repo.discussions.opened') }}
          {{ new Date(discussion.createdAt).toLocaleString() }}
          ·
          {{ t('repo.discussions.updated') }}
          {{ new Date(discussion.updatedAt).toLocaleString() }}
        </p>

        <UCard v-if="discussion.body">
          <RepoMarkdown :source="discussion.body" />
        </UCard>
      </div>

      <UCard>
        <template #header>
          <h2 class="font-semibold">
            {{ t('repo.discussions.commentsTitle') }}
          </h2>
        </template>

        <p
          v-if="!comments.length"
          class="text-sm text-[var(--ogb-text-muted)]"
        >
          {{ t('repo.discussions.noComments') }}
        </p>

        <ul
          v-else
          class="mb-6 divide-y"
          style="border-color: var(--ogb-border);"
        >
          <li
            v-for="comment in comments"
            :key="comment.id"
            class="py-4 first:pt-0 last:pb-0"
          >
            <div class="mb-2 flex items-center justify-between gap-2 text-xs text-[var(--ogb-text-muted)]">
              <span class="font-medium text-[var(--ogb-text)]">
                {{ memberLabel(comment.authorUserId) }}
              </span>
              <span>
                {{ new Date(comment.createdAt).toLocaleString() }}
                <span v-if="comment.editedAt"> · {{ t('repo.discussions.edited') }}</span>
              </span>
            </div>
            <p
              v-if="comment.isDeleted"
              class="text-sm italic text-[var(--ogb-text-muted)]"
            >
              {{ t('repo.discussions.commentDeleted') }}
            </p>
            <template v-else>
              <p
                v-if="comment.anchor"
                class="mb-2 font-mono text-xs text-[var(--ogb-text-muted)]"
              >
                {{ comment.anchor.filePath }}:{{ comment.anchor.line }}
                <span v-if="comment.anchor.resolution?.kind !== 'located'">
                  ({{ t('repo.discussions.anchorOutdated') }})
                </span>
              </p>
              <RepoMarkdown :source="comment.bodyMarkdown" />
            </template>
          </li>
        </ul>

        <form
          class="space-y-3 border-t pt-4"
          style="border-color: var(--ogb-border);"
          @submit.prevent="postComment"
        >
          <p
            v-if="isClosed"
            class="text-sm text-[var(--ogb-text-muted)]"
          >
            {{ t('repo.discussions.reopenHint') }}
          </p>
          <UFormField :label="t('repo.discussions.fields.comment')">
            <UTextarea
              v-model="commentBody"
              :rows="4"
              :placeholder="t('repo.discussions.commentPlaceholder')"
            />
          </UFormField>
          <UAlert
            v-if="postError"
            color="error"
            variant="subtle"
            :description="postError"
          />
          <UButton
            type="submit"
            :loading="posting"
            :disabled="!commentBody.trim()"
          >
            {{ auth.isAuthenticated ? t('repo.discussions.postComment') : t('nav.signIn') }}
          </UButton>
        </form>
      </UCard>
    </template>
  </div>
</template>
