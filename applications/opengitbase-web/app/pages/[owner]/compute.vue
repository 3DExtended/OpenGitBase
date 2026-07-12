<script setup lang="ts">
import type {
  ComputeNodeDto,
  ComputeNodeEnrollmentDto,
} from '~/utils/api'

definePageMeta({ middleware: 'auth' })

const route = useRoute()
const api = useApi()
const { t } = useI18n()

const orgSlug = computed(() => String(route.params.owner))
const organizationId = ref<string | null>(null)
const nodes = ref<ComputeNodeDto[]>([])
const enrollments = ref<ComputeNodeEnrollmentDto[]>([])
const loading = ref(true)
const error = ref<string | null>(null)
const forbidden = ref(false)

const createNodeId = ref('org-compute-1')
const createHostingScope = ref(0)
const createMaxConcurrentJobs = ref(1)
const createMaxCpu = ref(1)
const createMaxMemoryGiB = ref(2)
const createLoading = ref(false)
const createdEnrollmentToken = ref<string | null>(null)
const createdEnrollmentNodeId = ref<string | null>(null)

const editingNodeId = ref<string | null>(null)
const editMaxConcurrentJobs = ref(1)
const editMaxCpu = ref(1)
const editMaxMemoryGiB = ref(2)
const editLoading = ref(false)

const hostingScopeOptions = [
  { label: t('org.compute.hostingScope.ownOrgOnly'), value: 0 },
  { label: t('org.compute.hostingScope.crossOrgAllowed'), value: 1 },
]

const createdEnrollmentOverrideSnippet = computed(() => {
  if (!createdEnrollmentToken.value || !createdEnrollmentNodeId.value) return null
  const nodeId = createdEnrollmentNodeId.value
  return [
    'services:',
    `  ${nodeId}:`,
    '    environment:',
    `      ComputeAgent__EnrollmentToken: "${createdEnrollmentToken.value}"`,
    `      ComputeAgent__NodeId: "${nodeId}"`,
  ].join('\n')
})

function formatBytes(value: number) {
  if (value <= 0) return '0 GiB'
  return `${(value / (1024 ** 3)).toFixed(2)} GiB`
}

function hostingScopeLabel(scope: number) {
  return scope === 1
    ? t('org.compute.hostingScope.crossOrgAllowed')
    : t('org.compute.hostingScope.ownOrgOnly')
}

function startEdit(node: ComputeNodeDto) {
  editingNodeId.value = node.id
  editMaxConcurrentJobs.value = node.maxConcurrentJobs
  editMaxCpu.value = node.maxCpu
  editMaxMemoryGiB.value = Math.max(1, Math.round(node.maxMemoryBytes / (1024 ** 3)))
}

async function loadPage() {
  loading.value = true
  error.value = null
  try {
    const orgResult = await api.organizations.getBySlug(orgSlug.value)
    if (!orgResult.data) {
      error.value = t('org.compute.notFound')
      return
    }
    organizationId.value = orgResult.data.id
    const [nodesResult, enrollmentsResult] = await Promise.all([
      api.organizations.compute.listNodes(orgResult.data.id),
      api.organizations.compute.listEnrollments(orgResult.data.id),
    ])
    if (nodesResult.status === 403 || enrollmentsResult.status === 403) {
      forbidden.value = true
      error.value = t('org.compute.ownerOnly')
      return
    }
    nodes.value = nodesResult.data ?? []
    enrollments.value = enrollmentsResult.data ?? []
  }
  catch (e) {
    error.value = e instanceof Error ? e.message : t('org.compute.loadFailed')
  }
  finally {
    loading.value = false
  }
}

async function createEnrollment() {
  if (!organizationId.value) return
  createLoading.value = true
  createdEnrollmentToken.value = null
  createdEnrollmentNodeId.value = null
  try {
    const result = await api.organizations.compute.createEnrollment(organizationId.value, {
      nodeId: createNodeId.value,
      hostingScope: createHostingScope.value,
      maxConcurrentJobs: createMaxConcurrentJobs.value,
      maxCpu: createMaxCpu.value,
      maxMemoryBytes: createMaxMemoryGiB.value * 1024 ** 3,
    })
    if (result.error || !result.data?.enrollmentToken) {
      error.value = result.error ?? t('org.compute.createFailed')
      return
    }
    createdEnrollmentToken.value = result.data.enrollmentToken
    createdEnrollmentNodeId.value = result.data.nodeId
    await loadPage()
  }
  finally {
    createLoading.value = false
  }
}

async function saveCapacity(nodeId: string) {
  if (!organizationId.value) return
  editLoading.value = true
  error.value = null
  try {
    const result = await api.organizations.compute.updateCapacity(organizationId.value, nodeId, {
      maxConcurrentJobs: editMaxConcurrentJobs.value,
      maxCpu: editMaxCpu.value,
      maxMemoryBytes: editMaxMemoryGiB.value * 1024 ** 3,
    })
    if (result.error) {
      error.value = result.error
      return
    }
    editingNodeId.value = null
    await loadPage()
  }
  finally {
    editLoading.value = false
  }
}

onMounted(loadPage)
</script>

<template>
  <div class="mx-auto max-w-3xl space-y-6">
    <UButton
      :to="`/${orgSlug}`"
      variant="ghost"
      icon="i-lucide-arrow-left"
      size="sm"
    >
      {{ orgSlug }}
    </UButton>

    <div>
      <h1 class="text-2xl font-semibold">
        {{ t('org.compute.title') }}
      </h1>
      <p class="mt-1 text-sm text-[var(--ogb-text-muted)]">
        {{ t('org.compute.description') }}
        <NuxtLink
          to="/docs/ci/compute-nodes"
          class="text-[var(--ogb-accent)] hover:underline"
        >
          {{ t('org.compute.docsLink') }}
        </NuxtLink>
      </p>
    </div>

    <UAlert
      v-if="error"
      color="error"
      variant="subtle"
      :title="error"
    />

    <template v-if="!forbidden && !loading">
      <UCard>
        <template #header>
          <h2 class="font-semibold">
            {{ t('org.compute.nodesTitle') }}
          </h2>
        </template>
        <div
          v-if="!nodes.length"
          class="text-sm text-[var(--ogb-text-muted)]"
        >
          {{ t('org.compute.nodesEmpty') }}
        </div>
        <div
          v-for="node in nodes"
          :key="node.id"
          class="border-b py-4 last:border-b-0"
          style="border-color: var(--ogb-border);"
        >
          <div class="flex flex-wrap items-center gap-2">
            <span class="font-medium">{{ node.nodeId }}</span>
            <UBadge
              :color="node.isHealthy ? 'success' : 'warning'"
              variant="subtle"
            >
              {{ node.isHealthy ? t('org.compute.healthy') : t('org.compute.unhealthy') }}
            </UBadge>
            <UBadge
              color="neutral"
              variant="subtle"
            >
              {{ hostingScopeLabel(node.hostingScope) }}
            </UBadge>
          </div>
          <div class="mt-1 text-xs text-[var(--ogb-text-muted)]">
            {{ node.runningJobs }}/{{ node.maxConcurrentJobs }} jobs ·
            {{ node.maxCpu }} vCPU · {{ formatBytes(node.maxMemoryBytes) }}
          </div>
          <form
            v-if="editingNodeId === node.id"
            class="mt-3 grid gap-3 md:grid-cols-3"
            @submit.prevent="saveCapacity(node.id)"
          >
            <UFormField :label="t('org.compute.maxConcurrentJobsLabel')">
              <UInput
                v-model.number="editMaxConcurrentJobs"
                type="number"
                min="1"
              />
            </UFormField>
            <UFormField :label="t('org.compute.maxCpuLabel')">
              <UInput
                v-model.number="editMaxCpu"
                type="number"
                min="1"
              />
            </UFormField>
            <UFormField :label="t('org.compute.maxMemoryGiBLabel')">
              <UInput
                v-model.number="editMaxMemoryGiB"
                type="number"
                min="1"
              />
            </UFormField>
            <div class="flex gap-2 md:col-span-3">
              <UButton
                type="submit"
                size="sm"
                :loading="editLoading"
              >
                {{ t('org.compute.saveCapacity') }}
              </UButton>
              <UButton
                size="sm"
                variant="ghost"
                @click="editingNodeId = null"
              >
                {{ t('org.compute.cancel') }}
              </UButton>
            </div>
          </form>
          <UButton
            v-else
            class="mt-2"
            size="xs"
            variant="soft"
            @click="startEdit(node)"
          >
            {{ t('org.compute.editCapacity') }}
          </UButton>
        </div>
      </UCard>

      <UCard>
        <template #header>
          <h2 class="font-semibold">
            {{ t('org.compute.enrollmentsTitle') }}
          </h2>
        </template>
        <form
          class="grid gap-3 md:grid-cols-2"
          @submit.prevent="createEnrollment"
        >
          <UFormField
            :label="t('org.compute.nodeIdLabel')"
            required
          >
            <UInput v-model="createNodeId" />
          </UFormField>
          <UFormField :label="t('org.compute.hostingScopeLabel')">
            <USelect
              v-model="createHostingScope"
              :items="hostingScopeOptions"
            />
          </UFormField>
          <UFormField
            :label="t('org.compute.maxConcurrentJobsLabel')"
            required
          >
            <UInput
              v-model.number="createMaxConcurrentJobs"
              type="number"
              min="1"
            />
          </UFormField>
          <UFormField
            :label="t('org.compute.maxCpuLabel')"
            required
          >
            <UInput
              v-model.number="createMaxCpu"
              type="number"
              min="1"
            />
          </UFormField>
          <UFormField
            :label="t('org.compute.maxMemoryGiBLabel')"
            required
          >
            <UInput
              v-model.number="createMaxMemoryGiB"
              type="number"
              min="1"
            />
          </UFormField>
          <div class="flex items-end md:col-span-2">
            <UButton
              type="submit"
              :loading="createLoading"
            >
              {{ t('org.compute.createEnrollment') }}
            </UButton>
          </div>
        </form>

        <UAlert
          v-if="createdEnrollmentToken && createdEnrollmentNodeId"
          class="mt-4"
          color="warning"
          variant="subtle"
          :title="t('org.compute.tokenTitle')"
        >
          <template #description>
            <pre class="overflow-x-auto rounded bg-[var(--ogb-surface-muted)] p-2 text-xs"><code>{{ createdEnrollmentOverrideSnippet }}</code></pre>
          </template>
        </UAlert>

        <div class="mt-4 space-y-2">
          <UCard
            v-for="enrollment in enrollments"
            :key="enrollment.id"
            class="bg-[var(--ogb-bg)]"
          >
            <div class="flex items-center gap-2">
              <span class="font-medium">{{ enrollment.nodeId }}</span>
              <UBadge
                :color="enrollment.consumedAt ? 'neutral' : 'success'"
                variant="subtle"
              >
                {{ enrollment.consumedAt ? t('org.compute.consumed') : t('org.compute.active') }}
              </UBadge>
            </div>
            <div class="mt-1 text-xs text-[var(--ogb-text-muted)]">
              {{ hostingScopeLabel(enrollment.hostingScope) }} ·
              {{ enrollment.maxConcurrentJobs }} jobs · {{ enrollment.maxCpu }} vCPU
            </div>
          </UCard>
        </div>
      </UCard>
    </template>
  </div>
</template>
