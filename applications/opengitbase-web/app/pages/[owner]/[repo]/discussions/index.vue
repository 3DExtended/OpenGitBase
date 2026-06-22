<script setup lang="ts">
import type { Discussion, DiscussionStatus, RepositoryTag } from '~/utils/api'

const route = useRoute()
const auth = useAuth()
const { t } = useI18n()
const api = useApi()
const { owner, repoSlug, loading, notFound, loadRepo } = useRepoMetadata()

const discussions = ref<Discussion[]>([])
const tags = ref<RepositoryTag[]>([])
const listLoading = ref(false)
const forbidden = ref(false)
const signInRequired = ref(false)

const showCreate = ref(false)
const createTitle = ref('')
const createBody = ref('')
const createTagIds = ref<string[]>([])
const creating = ref(false)
const createError = ref<string | null>(null)

const statusFilter = ref<DiscussionStatus | 'all'>('all')
const tagFilter = ref<string | 'all'>('all')

const statusOptions = computed(() => [
  { label: t('repo.discussions.filters.allStatuses'), value: 'all' },
  { label: t('repo.discussions.status.open'), value: 'Open' as const },
  { label: t('repo.discussions.status.engaged'), value: 'Engaged' as const },
  { label: t('repo.discussions.status.resolved'), value: 'Resolved' as const },
  { label: t('repo.discussions.status.dismissed'), value: 'Dismissed' as const },
])

const tagOptions = computed(() => [
  { label: t('repo.discussions.filters.allTags'), value: 'all' },
  ...tags.value.map(tag => ({ label: tag.name, value: tag.id })),
])

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
  listLoading.value = true
  forbidden.value = false
  signInRequired.value = false

  const params: { status?: DiscussionStatus, tagId?: string } = {}
  if (statusFilter.value !== 'all') {
    params.status = statusFilter.value
  }
  if (tagFilter.value !== 'all') {
    params.tagId = tagFilter.value
  }

  const result = await api.discussions.list(owner.value, repoSlug.value, params)
  if (result.status === 403) {
    forbidden.value = true
  }
  else if (result.status === 401) {
    signInRequired.value = true
  }
  else {
    discussions.value = result.data ?? []
  }
  listLoading.value = false
}

async function loadTags(): Promise<void> {
  const result = await api.discussions.tags.list(owner.value, repoSlug.value)
  tags.value = result.data ?? []
}

async function createDiscussion(): Promise<void> {
  if (!auth.isAuthenticated) {
    await navigateTo('/sign-in')
    return
  }
  creating.value = true
  createError.value = null
  try {
    const result = await api.discussions.create(owner.value, repoSlug.value, {
      title: createTitle.value.trim(),
      body: createBody.value.trim() || null,
      tagIds: createTagIds.value,
    })
    if (result.error) {
      createError.value = result.error
      return
    }
    if (result.data) {
      await navigateTo(`/${owner.value}/${repoSlug.value}/discussions/${result.data.number}`)
    }
  }
  finally {
    creating.value = false
  }
}

function openCreate(): void {
  if (!auth.isAuthenticated) {
    void navigateTo('/sign-in')
    return
  }
  showCreate.value = true
}

watch([statusFilter, tagFilter], () => {
  void loadDiscussions()
})

onMounted(async () => {
  await loadRepo()
  if (!notFound.value) {
    await Promise.all([loadDiscussions(), loadTags()])
  }
})
</script>

<template>
  <div class="mx-auto max-w-3xl space-y-6">
    <UButton
      :to="`/${owner}/${repoSlug}`"
      variant="ghost"
      icon="i-lucide-arrow-left"
      size="sm"
    >
      {{ owner }}/{{ repoSlug }}
    </UButton>

    <div class="flex flex-wrap items-center justify-between gap-4">
      <h1 class="text-2xl font-semibold">
        {{ t('repo.discussions.heading') }}
      </h1>
      <UButton
        icon="i-lucide-plus"
        @click="openCreate"
      >
        {{ t('repo.discussions.createButton') }}
      </UButton>
    </div>

    <div
      v-if="loading"
      class="text-sm text-[var(--ogb-text-muted)]"
    >
      {{ t('common.loading') }}
    </div>

    <UCard v-else-if="notFound">
      <p class="text-sm text-[var(--ogb-text-muted)]">
        {{ t('repo.notFound') }}
      </p>
    </UCard>

    <UCard v-else-if="forbidden">
      <p class="text-sm text-[var(--ogb-text-muted)]">
        {{ t('repo.browse.forbidden') }}
      </p>
    </UCard>

    <template v-else>
      <div class="flex flex-wrap gap-3">
        <USelect
          v-model="statusFilter"
          :items="statusOptions"
          class="min-w-40"
        />
        <USelect
          v-if="tags.length"
          v-model="tagFilter"
          :items="tagOptions"
          class="min-w-40"
        />
      </div>

      <div
        v-if="listLoading"
        class="text-sm text-[var(--ogb-text-muted)]"
      >
        {{ t('common.loading') }}
      </div>

      <UCard v-else-if="!discussions.length">
        <p class="text-sm text-[var(--ogb-text-muted)]">
          {{ signInRequired ? t('repo.discussions.signInToView') : t('repo.discussions.empty') }}
        </p>
      </UCard>

      <ul
        v-else
        class="divide-y rounded-lg border"
        style="border-color: var(--ogb-border);"
      >
        <li
          v-for="discussion in discussions"
          :key="discussion.id"
        >
          <NuxtLink
            :to="`/${owner}/${repoSlug}/discussions/${discussion.number}`"
            class="flex flex-col gap-2 px-4 py-3 transition-colors hover:bg-[var(--ogb-bg)] sm:flex-row sm:items-center sm:justify-between"
          >
            <div class="min-w-0 space-y-1">
              <p class="truncate font-medium">
                <span class="text-[var(--ogb-text-muted)]">#{{ discussion.number }}</span>
                {{ discussion.title }}
              </p>
              <div class="flex flex-wrap items-center gap-2">
                <UBadge
                  :color="statusColor(discussion.status)"
                  variant="subtle"
                  size="sm"
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
            <p class="shrink-0 text-xs text-[var(--ogb-text-muted)]">
              {{ new Date(discussion.updatedAt).toLocaleString() }}
            </p>
          </NuxtLink>
        </li>
      </ul>
    </template>

    <UModal v-model:open="showCreate">
      <template #content>
        <UCard>
          <template #header>
            <h2 class="font-semibold">
              {{ t('repo.discussions.createTitle') }}
            </h2>
          </template>
          <form
            class="space-y-4"
            @submit.prevent="createDiscussion"
          >
            <UFormField
              :label="t('repo.discussions.fields.title')"
              required
            >
              <UInput
                v-model="createTitle"
                required
              />
            </UFormField>
            <UFormField :label="t('repo.discussions.fields.body')">
              <UTextarea
                v-model="createBody"
                :rows="4"
              />
            </UFormField>
            <UFormField
              v-if="tags.length"
              :label="t('repo.discussions.fields.tags')"
            >
              <USelectMenu
                v-model="createTagIds"
                :items="tags"
                value-key="id"
                label-key="name"
                multiple
              />
            </UFormField>
            <UAlert
              v-if="createError"
              color="error"
              variant="subtle"
              :description="createError"
            />
            <div class="flex justify-end gap-2">
              <UButton
                variant="ghost"
                @click="showCreate = false"
              >
                {{ t('common.cancel') }}
              </UButton>
              <UButton
                type="submit"
                :loading="creating"
              >
                {{ t('repo.discussions.createButton') }}
              </UButton>
            </div>
          </form>
        </UCard>
      </template>
    </UModal>
  </div>
</template>
