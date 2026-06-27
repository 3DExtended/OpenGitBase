<script setup lang="ts">
import type { MergeRequest } from '~/utils/api'

const route = useRoute()
const { t } = useI18n()
const api = useApi()

const owner = computed(() => String(route.params.owner))
const repoSlug = computed(() => String(route.params.repo))
const mergeRequests = ref<MergeRequest[]>([])
const loading = ref(true)
const error = ref<string | null>(null)

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

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  const result = await api.mergeRequests.list(owner.value, repoSlug.value)
  if (result.error) {
    error.value = result.error
  }
  mergeRequests.value = result.data ?? []
  loading.value = false
}

onMounted(() => {
  void load()
})
</script>

<template>
  <div class="mx-auto max-w-5xl space-y-4">
    <div class="flex items-center justify-between gap-3">
      <div>
        <p class="font-mono text-sm text-[var(--ogb-text-muted)]">
          {{ owner }}/{{ repoSlug }}
        </p>
        <h1 class="text-2xl font-semibold">
          {{ t('repo.mergeRequests.title') }}
        </h1>
      </div>
      <UButton
        :to="`/${owner}/${repoSlug}/merge-requests/new`"
        icon="i-lucide-git-pull-request-create"
      >
        {{ t('repo.mergeRequests.new') }}
      </UButton>
    </div>

    <UAlert
      v-if="error"
      color="error"
      variant="subtle"
      :description="error"
    />

    <UCard v-if="loading">
      <p class="text-sm text-[var(--ogb-text-muted)]">
        {{ t('common.loading') }}
      </p>
    </UCard>

    <UCard v-else-if="!mergeRequests.length">
      <p class="text-sm text-[var(--ogb-text-muted)]">
        {{ t('repo.mergeRequests.empty') }}
      </p>
    </UCard>

    <div
      v-else
      class="space-y-3"
    >
      <NuxtLink
        v-for="mr in mergeRequests"
        :key="mr.id"
        :to="`/${owner}/${repoSlug}/merge-requests/${mr.number}`"
        class="block rounded-lg border p-4 transition hover:bg-[var(--ogb-bg)]"
        style="border-color: var(--ogb-border);"
      >
        <div class="flex items-start justify-between gap-3">
          <div class="min-w-0">
            <p class="truncate font-medium">
              !{{ mr.number }} {{ mr.title }}
            </p>
            <p class="mt-1 text-xs text-[var(--ogb-text-muted)]">
              {{ mr.sourceRef }} → {{ mr.targetRef }}
            </p>
          </div>
          <UBadge
            :color="statusColor(mr.status)"
            variant="subtle"
          >
            {{ mr.status }}
          </UBadge>
        </div>
      </NuxtLink>
    </div>
  </div>
</template>
