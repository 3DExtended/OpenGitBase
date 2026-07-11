<script setup lang="ts">
import type { OrganizationStorageSettings, StorageNodeDto } from '~/utils/api'

definePageMeta({ middleware: 'auth' })

const route = useRoute()
const api = useApi()
const { t } = useI18n()

const orgSlug = computed(() => String(route.params.owner))
const organizationId = ref<string | null>(null)
const settings = ref<OrganizationStorageSettings | null>(null)
const nodes = ref<StorageNodeDto[]>([])
const loading = ref(true)
const saving = ref(false)
const error = ref<string | null>(null)

const placementPolicy = ref(0)
const selfHostPreference = ref(0)

const placementOptions = [
  { label: 'Inherit platform default', value: 0 },
  { label: 'Platform default', value: 1 },
  { label: 'Max self-host', value: 2 },
]

const selfHostOptions = [
  { label: 'Platform only', value: 0 },
  { label: 'Prefer self-host', value: 1 },
  { label: 'Require self-host', value: 2 },
]

function formatBytes(value: number) {
  if (value <= 0) return 'Unlimited'
  const gb = value / (1024 ** 3)
  return `${gb.toFixed(2)} GB`
}

onMounted(async () => {
  loading.value = true
  error.value = null
  try {
    const orgResult = await api.organizations.getBySlug(orgSlug.value)
    if (!orgResult.data) {
      error.value = 'Organization not found.'
      return
    }
    organizationId.value = orgResult.data.id
    const [settingsResult, nodesResult] = await Promise.all([
      api.organizations.storage.getSettings(orgResult.data.id),
      api.organizations.storage.listNodes(orgResult.data.id),
    ])
    settings.value = settingsResult.data
    nodes.value = nodesResult.data ?? []
    if (settingsResult.data) {
      placementPolicy.value = settingsResult.data.defaultPlacementPolicy
      selfHostPreference.value = settingsResult.data.defaultSelfHostPreference
    }
  }
  catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to load org storage settings.'
  }
  finally {
    loading.value = false
  }
})

async function saveSettings() {
  if (!organizationId.value) return
  saving.value = true
  error.value = null
  try {
    const result = await api.organizations.storage.updateSettings(organizationId.value, {
      defaultPlacementPolicy: placementPolicy.value,
      defaultSelfHostPreference: selfHostPreference.value,
    })
    settings.value = result.data
  }
  catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to save settings.'
  }
  finally {
    saving.value = false
  }
}
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

    <h1 class="text-2xl font-semibold">
      {{ t('org.storage.title') }}
    </h1>

    <UAlert
      v-if="error"
      color="error"
      variant="subtle"
      :title="error"
    />

    <UCard v-if="settings">
      <template #header>
        <h2 class="font-semibold">
          Quota credits
        </h2>
      </template>
      <dl class="grid gap-3 text-sm sm:grid-cols-3">
        <div>
          <dt class="text-[var(--ogb-text-muted)]">
            Platform limit
          </dt>
          <dd>{{ formatBytes(settings.platformBytesLimit) }}</dd>
        </div>
        <div>
          <dt class="text-[var(--ogb-text-muted)]">
            Contributed capacity
          </dt>
          <dd>{{ formatBytes(settings.contributedBytesCapacity) }}</dd>
        </div>
        <div>
          <dt class="text-[var(--ogb-text-muted)]">
            Effective limit
          </dt>
          <dd>{{ formatBytes(settings.bytesLimit) }}</dd>
        </div>
      </dl>
    </UCard>

    <UCard>
      <template #header>
        <h2 class="font-semibold">
          Placement defaults
        </h2>
      </template>
      <form
        class="grid gap-4"
        @submit.prevent="saveSettings"
      >
        <UFormField label="Default placement policy">
          <USelect
            v-model="placementPolicy"
            :items="placementOptions"
          />
        </UFormField>
        <UFormField label="Self-host preference">
          <USelect
            v-model="selfHostPreference"
            :items="selfHostOptions"
          />
        </UFormField>
        <UButton
          type="submit"
          :loading="saving"
        >
          Save settings
        </UButton>
      </form>
    </UCard>

    <UCard>
      <template #header>
        <h2 class="font-semibold">
          Org-owned nodes
        </h2>
      </template>
      <div
        v-if="!nodes.length"
        class="text-sm text-[var(--ogb-text-muted)]"
      >
        No registered storage nodes yet.
      </div>
      <div
        v-for="node in nodes"
        :key="node.id"
        class="border-b py-3 last:border-b-0"
        style="border-color: var(--ogb-border);"
      >
        <div class="font-medium">
          {{ node.nodeId }}
        </div>
        <div class="mt-1 text-xs text-[var(--ogb-text-muted)]">
          used {{ formatBytes(node.usedBytes ?? 0) }} / max {{ formatBytes(node.maxBytes ?? 0) }}
          · hosting scope {{ node.hostingScope === 1 ? 'Cross-org allowed' : 'Own org only' }}
        </div>
      </div>
    </UCard>
  </div>
</template>
