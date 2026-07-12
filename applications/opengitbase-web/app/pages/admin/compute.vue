<script setup lang="ts">
import type {
  ComputeNodeDto,
  ComputeNodeEnrollmentDto,
} from '~/utils/api'

definePageMeta({ middleware: 'admin' })

const { t } = useI18n()
const api = useApi()

useHead({ title: t('admin.compute.title') })

const loading = ref(true)
const error = ref<string | null>(null)

const nodes = ref<ComputeNodeDto[]>([])
const enrollments = ref<ComputeNodeEnrollmentDto[]>([])

const createNodeId = ref('compute-agent-1')
const createMaxConcurrentJobs = ref(2)
const createMaxCpu = ref(2)
const createMaxMemoryGiB = ref(2)
const createLoading = ref(false)
const createdEnrollmentToken = ref<string | null>(null)
const createdEnrollmentNodeId = ref<string | null>(null)

const createdEnrollmentOverrideSnippet = computed(() => {
  if (!createdEnrollmentToken.value || !createdEnrollmentNodeId.value) return null
  const nodeId = createdEnrollmentNodeId.value
  return [
    'services:',
    `  ${nodeId}:`,
    '    environment:',
    `      ComputeAgent__EnrollmentToken: "${createdEnrollmentToken.value}"`,
  ].join('\n')
})

const healthyNodeCount = computed(() => nodes.value.filter(node => node.isHealthy).length)

function formatBytes(value: number) {
  const gb = value / (1024 ** 3)
  return `${gb.toFixed(2)} GiB`
}

async function refreshAll() {
  loading.value = true
  error.value = null
  try {
    const [nodesResult, enrollmentsResult] = await Promise.all([
      api.admin.computeNodes.list(),
      api.admin.computeEnrollments.list(),
    ])
    nodes.value = nodesResult.data ?? []
    enrollments.value = enrollmentsResult.data ?? []
  }
  catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to load admin compute data.'
  }
  finally {
    loading.value = false
  }
}

async function createEnrollment() {
  createLoading.value = true
  createdEnrollmentToken.value = null
  createdEnrollmentNodeId.value = null
  try {
    const result = await api.admin.computeEnrollments.create({
      nodeId: createNodeId.value,
      maxConcurrentJobs: createMaxConcurrentJobs.value,
      maxCpu: createMaxCpu.value,
      maxMemoryBytes: createMaxMemoryGiB.value * 1024 ** 3,
    })
    if (result.error || !result.data?.enrollmentToken) {
      error.value = result.error ?? 'Failed to create enrollment.'
      return
    }
    createdEnrollmentToken.value = result.data.enrollmentToken
    createdEnrollmentNodeId.value = result.data.nodeId
    await refreshAll()
  }
  finally {
    createLoading.value = false
  }
}

onMounted(refreshAll)
</script>

<template>
  <div class="mx-auto max-w-5xl space-y-6">
    <UButton
      to="/admin"
      variant="ghost"
      size="sm"
      icon="i-lucide-arrow-left"
      class="-ml-2"
    >
      {{ t('admin.nav') }}
    </UButton>

    <div class="flex flex-wrap items-start justify-between gap-3">
      <div>
        <h1 class="text-2xl font-semibold">
          {{ t('admin.compute.title') }}
        </h1>
        <p class="mt-1 text-sm text-[var(--ogb-text-muted)]">
          {{ t('admin.compute.description') }}
        </p>
      </div>

      <UButton
        icon="i-lucide-refresh-cw"
        variant="soft"
        :loading="loading"
        @click="refreshAll"
      >
        {{ t('admin.replication.refresh') }}
      </UButton>
    </div>

    <UAlert
      v-if="error"
      color="error"
      variant="subtle"
      :description="error"
    />

    <UAlert
      color="info"
      variant="subtle"
      :title="t('admin.compute.bootstrapTitle')"
      :description="t('admin.compute.bootstrapDescription')"
    />

    <UCard>
      <template #header>
        <div class="flex items-center justify-between gap-3">
          <h2 class="font-semibold">
            {{ t('admin.compute.nodesTitle') }}
          </h2>
          <UBadge
            color="neutral"
            variant="subtle"
          >
            {{ healthyNodeCount }}/{{ nodes.length }} {{ t('admin.compute.healthy') }}
          </UBadge>
        </div>
      </template>

      <div
        v-if="loading"
        class="text-sm text-[var(--ogb-text-muted)]"
      >
        Loading…
      </div>

      <div
        v-else-if="!nodes.length"
        class="text-sm text-[var(--ogb-text-muted)]"
      >
        {{ t('admin.compute.nodesEmpty') }}
      </div>

      <div
        v-else
        class="space-y-2"
      >
        <UCard
          v-for="node in nodes"
          :key="node.id"
          class="bg-[var(--ogb-bg)]"
        >
          <div class="flex flex-wrap items-center justify-between gap-3">
            <div class="min-w-0">
              <div class="flex items-center gap-2">
                <span class="truncate font-medium">{{ node.nodeId }}</span>
                <UBadge
                  :color="node.isHealthy ? 'success' : 'warning'"
                  variant="subtle"
                >
                  {{ node.isHealthy ? t('admin.compute.healthy') : t('admin.compute.unhealthy') }}
                </UBadge>
              </div>
              <div class="mt-1 text-xs text-[var(--ogb-text-muted)]">
                {{ t('admin.compute.capacity') }}:
                {{ node.runningJobs }}/{{ node.maxConcurrentJobs }} jobs ·
                {{ node.maxCpu }} vCPU · {{ formatBytes(node.maxMemoryBytes) }}
              </div>
              <div
                v-if="node.lastHeartbeatAt"
                class="mt-1 text-xs text-[var(--ogb-text-muted)]"
              >
                {{ t('admin.compute.lastHeartbeat') }}: {{ node.lastHeartbeatAt }}
              </div>
            </div>
          </div>
        </UCard>
      </div>
    </UCard>

    <UCard>
      <template #header>
        <h2 class="font-semibold">
          {{ t('admin.compute.enrollmentsTitle') }}
        </h2>
      </template>

      <form
        class="grid gap-3 md:grid-cols-2 lg:grid-cols-4"
        @submit.prevent="createEnrollment"
      >
        <UFormField
          :label="t('admin.compute.nodeIdLabel')"
          required
        >
          <UInput
            v-model="createNodeId"
            placeholder="compute-agent-1"
          />
        </UFormField>
        <UFormField
          :label="t('admin.compute.maxConcurrentJobsLabel')"
          required
        >
          <UInput
            v-model.number="createMaxConcurrentJobs"
            type="number"
            min="1"
          />
        </UFormField>
        <UFormField
          :label="t('admin.compute.maxCpuLabel')"
          required
        >
          <UInput
            v-model.number="createMaxCpu"
            type="number"
            min="1"
          />
        </UFormField>
        <UFormField
          :label="t('admin.compute.maxMemoryGiBLabel')"
          required
        >
          <UInput
            v-model.number="createMaxMemoryGiB"
            type="number"
            min="1"
          />
        </UFormField>
        <div class="flex items-end md:col-span-2 lg:col-span-4">
          <UButton
            type="submit"
            :loading="createLoading"
          >
            {{ t('admin.compute.createEnrollment') }}
          </UButton>
        </div>
      </form>

      <UAlert
        v-if="createdEnrollmentToken && createdEnrollmentNodeId"
        class="mt-4"
        color="warning"
        variant="subtle"
        :title="t('admin.compute.tokenTitle')"
      >
        <template #description>
          <div class="space-y-2">
            <div class="text-sm text-[var(--ogb-text-muted)]">
              {{ t('admin.compute.tokenHint') }}
            </div>
            <pre class="overflow-x-auto rounded bg-[var(--ogb-surface-muted)] p-2 text-xs"><code>{{ createdEnrollmentOverrideSnippet }}</code></pre>
            <div class="text-sm text-[var(--ogb-text-muted)]">
              {{ t('admin.compute.tokenOnce') }}
            </div>
          </div>
        </template>
      </UAlert>

      <div class="mt-4 space-y-2">
        <div
          v-if="!enrollments.length"
          class="text-sm text-[var(--ogb-text-muted)]"
        >
          {{ t('admin.compute.enrollmentsEmpty') }}
        </div>

        <UCard
          v-for="enrollment in enrollments"
          :key="enrollment.id"
          class="bg-[var(--ogb-bg)]"
        >
          <div class="flex flex-wrap items-start justify-between gap-3">
            <div class="min-w-0">
              <div class="flex items-center gap-2">
                <span class="font-medium">{{ enrollment.nodeId }}</span>
                <UBadge
                  :color="enrollment.consumedAt ? 'neutral' : 'success'"
                  variant="subtle"
                >
                  {{ enrollment.consumedAt ? t('admin.compute.consumed') : t('admin.compute.active') }}
                </UBadge>
              </div>
              <div class="mt-1 text-xs text-[var(--ogb-text-muted)]">
                {{ enrollment.maxConcurrentJobs }} jobs · {{ enrollment.maxCpu }} vCPU ·
                {{ formatBytes(enrollment.maxMemoryBytes) }}
              </div>
              <div class="mt-1 text-xs text-[var(--ogb-text-muted)]">
                {{ t('admin.compute.expires') }}: {{ enrollment.expiresAt }}
              </div>
            </div>
          </div>
        </UCard>
      </div>
    </UCard>
  </div>
</template>
