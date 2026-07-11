<script setup lang="ts">
import type { PipelineRun } from '~/utils/api'

const route = useRoute()
const { t } = useI18n()
const api = useApi()

const owner = computed(() => String(route.params.owner))
const repoSlug = computed(() => String(route.params.repo))

const runs = ref<PipelineRun[]>([])
const loading = ref(true)
const error = ref<string | null>(null)

function statusColor(status: PipelineRun['status']): 'neutral' | 'info' | 'success' | 'warning' | 'error' {
  switch (status) {
    case 'Queued':
      return 'neutral'
    case 'Running':
      return 'info'
    case 'Passed':
      return 'success'
    case 'Cancelled':
      return 'warning'
    case 'Failed':
      return 'error'
  }
}

async function loadRuns(): Promise<void> {
  loading.value = true
  error.value = null
  const result = await api.pipelines.list(owner.value, repoSlug.value)
  if (result.error) {
    error.value = result.error
  }
  runs.value = result.data ?? []
  loading.value = false
}

watch([owner, repoSlug], () => {
  void loadRuns()
})

onMounted(() => {
  void loadRuns()
})
</script>

<template>
  <div class="mx-auto max-w-5xl space-y-4">
    <div>
      <p class="font-mono text-sm text-[var(--ogb-text-muted)]">
        {{ owner }}/{{ repoSlug }}
      </p>
      <h1 class="text-2xl font-semibold">
        {{ t('repo.pipelines.title') }}
      </h1>
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

    <UCard
      v-else-if="!runs.length"
      class="space-y-4"
    >
      <p class="text-sm text-[var(--ogb-text-muted)]">
        {{ t('repo.pipelines.empty') }}
      </p>
      <p class="text-sm text-[var(--ogb-text-muted)]">
        {{ t('repo.pipelines.emptyDescription') }}
      </p>
      <UButton
        to="/docs/ci"
        variant="soft"
        icon="i-lucide-book-open"
      >
        {{ t('repo.pipelines.readDocs') }}
      </UButton>
    </UCard>

    <div
      v-else
      class="space-y-3"
    >
      <NuxtLink
        v-for="run in runs"
        :key="run.id"
        :to="`/${owner}/${repoSlug}/pipelines/${run.id}`"
        class="block rounded-lg border p-4 transition hover:bg-[var(--ogb-bg)]"
        style="border-color: var(--ogb-border);"
      >
        <div class="flex items-start justify-between gap-3">
          <div class="min-w-0">
            <p class="truncate font-medium">
              {{ run.ref }}
            </p>
            <p class="font-mono text-xs text-[var(--ogb-text-muted)]">
              {{ run.afterSha }}
            </p>
            <RelativeTime
              class="mt-1 text-xs text-[var(--ogb-text-muted)]"
              :iso="run.createdAt"
            />
          </div>
          <CollaborationStatusBadge
            :label="run.status"
            :color="statusColor(run.status)"
          />
        </div>
      </NuxtLink>
    </div>
  </div>
</template>
