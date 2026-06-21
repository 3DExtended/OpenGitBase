<script setup lang="ts">
definePageMeta({ middleware: 'admin' })

const { t } = useI18n()
const api = useApi()

useHead({ title: t('admin.storage.title') })

type StorageNode = {
  id: string
  nodeId: string
  internalHost: string
  internalSshPort: number
  internalHttpPort: number
  isHealthy: boolean
  registeredAt: string
  lastHeartbeatAt?: string | null
  freeBytesAvailable: number
  totalBytesAvailable: number
  certificateThumbprint?: string
}

type Enrollment = {
  id: string
  nodeId: string
  createdAt: string
  expiresAt: string
  consumedAt?: string | null
}

const loading = ref(true)
const error = ref<string | null>(null)

const nodes = ref<StorageNode[]>([])
const enrollments = ref<Enrollment[]>([])

const createNodeId = ref('storage-1')
const createExpiresInHours = ref<number | null>(null)
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
    `      STORAGE_ENROLLMENT_TOKEN: "${createdEnrollmentToken.value}"`,
  ].join('\n')
})

const fleetPublicKey = ref<string | null>(null)
const generatedBootstrapToken = ref<string | null>(null)
const generateKeysLoading = ref(false)

async function refreshAll() {
  loading.value = true
  error.value = null
  try {
    const [nodesResult, enrollmentsResult, pubKeyResult] = await Promise.all([
      api.admin.storageNodes.list(),
      api.admin.storageEnrollments.list(),
      api.admin.fleet.getDispatcherSshPublicKey(),
    ])
    nodes.value = nodesResult.data ?? []
    enrollments.value = enrollmentsResult.data ?? []
    fleetPublicKey.value = pubKeyResult.data?.publicKey ?? null
  }
  catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to load admin storage data.'
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
    const result = await api.admin.storageEnrollments.create({
      nodeId: createNodeId.value,
      expiresInHours: createExpiresInHours.value ?? undefined,
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

async function generateFleetKeys() {
  generateKeysLoading.value = true
  generatedBootstrapToken.value = null
  try {
    const result = await api.admin.fleet.generateDispatcherSshKeys()
    if (result.error || !result.data) {
      error.value = result.error ?? 'Failed to generate fleet keys.'
      return
    }
    fleetPublicKey.value = result.data.dispatcherSshPublicKey
    generatedBootstrapToken.value = result.data.fleetBootstrapToken
  }
  finally {
    generateKeysLoading.value = false
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
          {{ t('admin.storage.title') }}
        </h1>
        <p class="mt-1 text-sm text-[var(--ogb-text-muted)]">
          {{ t('admin.storage.description') }}
        </p>
        <p class="mt-2 text-xs text-[var(--ogb-text-muted)]">
          RF=3 replication requires three healthy storage nodes. Per-repository detail:
          <code>GET /admin/repositories/{id}/replication</code>.
          Fleet node summary:
          <code>GET /admin/storage-nodes/replication-summary</code>.
        </p>
      </div>

      <UButton
        icon="i-lucide-refresh-cw"
        variant="soft"
        :loading="loading"
        @click="refreshAll"
      >
        Refresh
      </UButton>
    </div>

    <UAlert
      v-if="error"
      color="error"
      variant="subtle"
      :description="error"
    />

    <UCard>
      <template #header>
        <div class="flex items-center justify-between gap-3">
          <h2 class="font-semibold">
            Storage nodes
          </h2>
          <UBadge
            color="neutral"
            variant="subtle"
          >
            {{ nodes.length }}
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
        No storage nodes registered yet.
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
                  {{ node.isHealthy ? 'Healthy' : 'Unhealthy' }}
                </UBadge>
              </div>
              <div class="mt-1 text-xs text-[var(--ogb-text-muted)]">
                {{ node.internalHost }}:{{ node.internalHttpPort }} (ssh {{ node.internalSshPort }})
              </div>
              <div
                v-if="node.certificateThumbprint"
                class="mt-1 text-xs text-[var(--ogb-text-muted)]"
              >
                cert: <code class="break-all">{{ node.certificateThumbprint }}</code>
              </div>
            </div>
          </div>
        </UCard>
      </div>
    </UCard>

    <UCard>
      <template #header>
        <h2 class="font-semibold">
          Storage enrollments
        </h2>
      </template>

      <form
        class="grid gap-3 md:grid-cols-3"
        @submit.prevent="createEnrollment"
      >
        <UFormField label="Node ID" required>
          <UInput v-model="createNodeId" placeholder="storage-1" />
        </UFormField>
        <UFormField label="Expires in hours (optional)">
          <UInput
            v-model.number="createExpiresInHours"
            type="number"
            min="1"
            placeholder="168"
          />
        </UFormField>
        <div class="flex items-end">
          <UButton
            type="submit"
            :loading="createLoading"
          >
            Create enrollment
          </UButton>
        </div>
      </form>

      <UAlert
        v-if="createdEnrollmentToken && createdEnrollmentNodeId"
        class="mt-4"
        color="warning"
        variant="subtle"
        title="Enrollment token (shown once)"
      >
        <template #description>
          <div class="space-y-2">
            <div class="text-sm text-[var(--ogb-text-muted)]">
              Copy this into <code>docker-compose.override.yml</code> (and don’t commit it):
            </div>
            <pre class="overflow-x-auto rounded bg-[var(--ogb-surface-muted)] p-2 text-xs"><code>{{ createdEnrollmentOverrideSnippet }}</code></pre>
            <div class="text-sm text-[var(--ogb-text-muted)]">
              You won’t be able to view this token again after leaving the page.
            </div>
          </div>
        </template>
      </UAlert>

      <div class="mt-4 space-y-2">
        <div
          v-if="!enrollments.length"
          class="text-sm text-[var(--ogb-text-muted)]"
        >
          No enrollments created yet.
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
                  {{ enrollment.consumedAt ? 'Consumed' : 'Active' }}
                </UBadge>
              </div>
              <div class="mt-1 text-xs text-[var(--ogb-text-muted)]">
                expires: {{ enrollment.expiresAt }}
              </div>
              <div class="mt-1 text-xs text-[var(--ogb-text-muted)]">
                created: {{ enrollment.createdAt }}
              </div>
            </div>
          </div>
        </UCard>
      </div>
    </UCard>

    <UCard>
      <template #header>
        <h2 class="font-semibold">
          Fleet dispatcher SSH keys
        </h2>
      </template>

      <div class="space-y-3">
        <div v-if="fleetPublicKey">
          <div class="text-sm text-[var(--ogb-text-muted)]">
            Current public key:
          </div>
          <code class="mt-1 block break-all text-xs">{{ fleetPublicKey }}</code>
        </div>

        <UButton
          color="primary"
          variant="soft"
          :loading="generateKeysLoading"
          @click="generateFleetKeys"
        >
          Generate new fleet keys
        </UButton>

        <UAlert
          v-if="generatedBootstrapToken"
          color="warning"
          variant="subtle"
          title="Bootstrap token (shown once)"
          :description="generatedBootstrapToken"
        />
      </div>
    </UCard>
  </div>
</template>

