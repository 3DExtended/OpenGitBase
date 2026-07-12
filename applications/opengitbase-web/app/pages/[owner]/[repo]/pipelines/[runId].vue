<script setup lang="ts">
import type { PipelineJob, PipelineJobLog, PipelineRun } from '~/utils/api'

const route = useRoute()
const { t } = useI18n()
const api = useApi()

const owner = computed(() => String(route.params.owner))
const repoSlug = computed(() => String(route.params.repo))
const runId = computed(() => String(route.params.runId))

const run = ref<PipelineRun | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)
const selectedJobId = ref<string | null>(null)
const logsByJob = ref<Record<string, PipelineJobLog[]>>({})
const pollingHandle = ref<ReturnType<typeof setInterval> | null>(null)
const logStream = ref<EventSource | null>(null)
const streamingJobId = ref<string | null>(null)

const selectedJob = computed<PipelineJob | null>(() => {
  if (!run.value || !selectedJobId.value) {
    return null
  }
  return run.value.jobs.find(job => job.id === selectedJobId.value) ?? null
})

const selectedLogs = computed(() => {
  if (!selectedJobId.value) {
    return []
  }
  return logsByJob.value[selectedJobId.value] ?? []
})

const logsBySection = computed(() => {
  const grouped = new Map<string, string[]>()
  for (const entry of selectedLogs.value) {
    const section = entry.section || 'general'
    const lines = grouped.get(section) ?? []
    lines.push(entry.line)
    grouped.set(section, lines)
  }
  return [...grouped.entries()]
})

const hasRunningJobs = computed(() => run.value?.jobs.some(job => job.status === 'Running') ?? false)

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

function jobStatusColor(status: PipelineJob['status']): 'neutral' | 'info' | 'success' | 'warning' | 'error' {
  switch (status) {
    case 'Queued':
    case 'Blocked':
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

function stopLogStream(): void {
  if (logStream.value) {
    logStream.value.close()
    logStream.value = null
  }
  streamingJobId.value = null
}

function appendStreamedLog(entry: PipelineJobLog): void {
  const jobId = selectedJobId.value
  if (!jobId) {
    return
  }
  const existing = logsByJob.value[jobId] ?? []
  if (existing.some(item => item.timestamp === entry.timestamp && item.line === entry.line && item.section === entry.section)) {
    return
  }
  logsByJob.value = {
    ...logsByJob.value,
    [jobId]: [...existing, entry],
  }
}

function startLogStream(jobId: string): void {
  if (typeof window === 'undefined' || !import.meta.client) {
    return
  }
  const job = run.value?.jobs.find(item => item.id === jobId)
  if (!job || job.status !== 'Running') {
    stopLogStream()
    return
  }
  if (streamingJobId.value === jobId && logStream.value) {
    return
  }
  stopLogStream()
  streamingJobId.value = jobId
  const lastTimestamp = (logsByJob.value[jobId] ?? [])
    .map(entry => entry.timestamp)
    .sort()
    .at(-1)
  const query = lastTimestamp ? `?after=${encodeURIComponent(lastTimestamp)}` : ''
  const source = new EventSource(`/pipeline/jobs/${encodeURIComponent(jobId)}/logs/stream${query}`)
  source.addEventListener('log', (event) => {
    try {
      const payload = JSON.parse((event as MessageEvent<string>).data) as PipelineJobLog
      appendStreamedLog(payload)
    }
    catch {
      // ignore malformed SSE payloads
    }
  })
  source.addEventListener('end', () => {
    stopLogStream()
    void loadRun(false)
  })
  logStream.value = source
}

function stopPolling(): void {
  if (pollingHandle.value) {
    clearInterval(pollingHandle.value)
    pollingHandle.value = null
  }
}

async function loadLogs(jobId: string): Promise<void> {
  const result = await api.pipelines.getJobLogs(jobId)
  logsByJob.value = {
    ...logsByJob.value,
    [jobId]: result.data ?? [],
  }
}

async function loadRun(loadJobLogs = false): Promise<void> {
  loading.value = true
  error.value = null
  const result = await api.pipelines.getRun(runId.value)
  run.value = result.data
  if (result.error) {
    error.value = result.error
  }
  if (run.value && !selectedJobId.value && run.value.jobs.length > 0) {
    selectedJobId.value = run.value.jobs[0]!.id
  }

  if (loadJobLogs && selectedJobId.value) {
    await loadLogs(selectedJobId.value)
  }
  loading.value = false
}

async function cancelJob(job: PipelineJob): Promise<void> {
  await api.pipelines.cancelJob(job.id)
  await loadRun(true)
}

watch([owner, repoSlug, runId], () => {
  stopPolling()
  stopLogStream()
  void loadRun(true)
})

watch(selectedJobId, (jobId) => {
  stopLogStream()
  if (!jobId) {
    return
  }
  void loadLogs(jobId).then(() => {
    startLogStream(jobId)
  })
})

watch(hasRunningJobs, (isRunning) => {
  stopPolling()
  if (!isRunning) {
    stopLogStream()
    return
  }
  if (selectedJobId.value) {
    startLogStream(selectedJobId.value)
  }
  pollingHandle.value = setInterval(() => {
    void loadRun(false)
  }, 4000)
}, { immediate: true })

onMounted(() => {
  void loadRun(true)
})

onBeforeUnmount(() => {
  stopPolling()
  stopLogStream()
})
</script>

<template>
  <div class="mx-auto max-w-6xl space-y-4">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <div>
        <p class="font-mono text-sm text-[var(--ogb-text-muted)]">
          {{ owner }}/{{ repoSlug }}
        </p>
        <h1 class="text-2xl font-semibold">
          {{ t('repo.pipelines.title') }}
        </h1>
      </div>
      <UButton
        :to="`/${owner}/${repoSlug}/pipelines`"
        variant="ghost"
        icon="i-lucide-arrow-left"
      >
        {{ t('repo.pipelines.title') }}
      </UButton>
    </div>

    <UCard v-if="loading || error || !run">
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
        {{ error ?? t('repo.pipelines.runNotFound') }}
      </p>
    </UCard>

    <template v-else>
      <UCard>
        <div class="flex flex-wrap items-center justify-between gap-2">
          <div>
            <p class="font-mono text-sm">
              {{ run.afterSha }}
            </p>
            <p class="text-sm text-[var(--ogb-text-muted)]">
              {{ run.ref }}
            </p>
          </div>
          <CollaborationStatusBadge
            :label="run.status"
            :color="statusColor(run.status)"
          />
        </div>
      </UCard>

      <div class="grid gap-4 lg:grid-cols-[1fr_2fr]">
        <UCard>
          <template #header>
            <h2 class="font-semibold">
              {{ t('repo.pipelines.jobs') }}
            </h2>
          </template>
          <div class="space-y-2">
            <button
              v-for="job in run.jobs"
              :key="job.id"
              type="button"
              class="w-full rounded border p-3 text-left transition hover:bg-[var(--ogb-bg)]"
              style="border-color: var(--ogb-border);"
              :class="{ 'ring-2 ring-[var(--ogb-accent)]': selectedJobId === job.id }"
              @click="selectedJobId = job.id"
            >
              <div class="flex items-center justify-between gap-2">
                <div>
                  <p class="font-medium">
                    {{ job.name }}
                  </p>
                  <p class="text-xs text-[var(--ogb-text-muted)]">
                    {{ job.stage }} · {{ job.runsOn }}
                  </p>
                </div>
                <div class="flex items-center gap-2">
                  <CollaborationStatusBadge
                    :label="job.status"
                    :color="jobStatusColor(job.status)"
                  />
                  <UButton
                    v-if="job.status === 'Running'"
                    size="xs"
                    color="warning"
                    variant="soft"
                    @click.stop="cancelJob(job)"
                  >
                    {{ t('repo.pipelines.cancelJob') }}
                  </UButton>
                </div>
              </div>
            </button>
          </div>
        </UCard>

        <UCard>
          <template #header>
            <div class="flex items-center justify-between gap-2">
              <h2 class="font-semibold">
                {{ t('repo.pipelines.logs') }}
              </h2>
              <span
                v-if="hasRunningJobs"
                class="text-xs text-[var(--ogb-text-muted)]"
              >
                {{ t('repo.pipelines.streamingHint') }}
              </span>
            </div>
          </template>
          <div
            v-if="!selectedJob"
            class="text-sm text-[var(--ogb-text-muted)]"
          >
            {{ t('repo.pipelines.noLogs') }}
          </div>
          <div
            v-else-if="!selectedLogs.length"
            class="text-sm text-[var(--ogb-text-muted)]"
          >
            {{ t('repo.pipelines.noLogs') }}
          </div>
          <div
            v-else
            class="max-h-[36rem] space-y-3 overflow-auto"
          >
            <div
              v-for="[section, lines] in logsBySection"
              :key="section"
              class="rounded border bg-[var(--ogb-bg)] p-3"
              style="border-color: var(--ogb-border);"
            >
              <h3 class="mb-2 font-mono text-xs uppercase tracking-wide text-[var(--ogb-text-muted)]">
                {{ section }}
              </h3>
              <pre class="font-mono text-xs whitespace-pre-wrap"><code>{{ lines.join('\n') }}</code></pre>
            </div>
          </div>
        </UCard>
      </div>
    </template>
  </div>
</template>
