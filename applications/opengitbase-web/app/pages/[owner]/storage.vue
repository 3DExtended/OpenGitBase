<script setup lang="ts">
import type {
  OrganizationStorageSettings,
  StorageNodeDto,
  StorageNodeEnrollmentDto,
} from '~/utils/api'
import {
  buildOrgStorageBootstrapDownloadScript,
  buildOrgStorageBootstrapInvocation,
  bytesToGibi,
  gibiToBytes,
} from '~/utils/orgStorageBootstrap'

definePageMeta({ middleware: 'auth' })

const route = useRoute()
const api = useApi()
const config = useRuntimeConfig()
const { t } = useI18n()

const orgSlug = computed(() => String(route.params.owner))
const organizationId = ref<string | null>(null)
const settings = ref<OrganizationStorageSettings | null>(null)
const nodes = ref<StorageNodeDto[]>([])
const enrollments = ref<StorageNodeEnrollmentDto[]>([])
const loading = ref(true)
const saving = ref(false)
const error = ref<string | null>(null)
const forbidden = ref(false)

const placementPolicy = ref(0)
const selfHostPreference = ref(0)

const createNodeId = ref('org-storage-1')
const createMaxGiB = ref(100)
const createHostingScope = ref(0)
const createLoading = ref(false)
const createdEnrollmentToken = ref<string | null>(null)
const createdEnrollmentNodeId = ref<string | null>(null)
const bootstrapInternalHost = ref('storage.example.com')

const editingNodeId = ref<string | null>(null)
const editMaxGiB = ref(1)
const editHostingScope = ref(0)
const editLoading = ref(false)

const placementOptions = computed(() => [
  { label: t('org.storage.placement.inherit'), value: 0 },
  { label: t('org.storage.placement.platformDefault'), value: 1 },
  { label: t('org.storage.placement.maxSelfHost'), value: 2 },
])

const selfHostOptions = computed(() => [
  { label: t('org.storage.selfHost.platformOnly'), value: 0 },
  { label: t('org.storage.selfHost.preferSelfHost'), value: 1 },
  { label: t('org.storage.selfHost.requireSelfHost'), value: 2 },
])

const apiBaseUrl = computed(() => {
  if (import.meta.client) {
    return `${window.location.origin}${config.public.apiBase}`
  }
  return String(config.public.apiBase)
})

const bootstrapCommand = computed(() => {
  if (!createdEnrollmentToken.value || !createdEnrollmentNodeId.value) {
    return null
  }
  return buildOrgStorageBootstrapInvocation({
    enrollmentToken: createdEnrollmentToken.value,
    nodeId: createdEnrollmentNodeId.value,
    apiUrl: apiBaseUrl.value,
    internalHost: bootstrapInternalHost.value,
  })
})

function startEdit(node: StorageNodeDto) {
  editingNodeId.value = node.id
  editMaxGiB.value = bytesToGibi(node.maxBytes ?? 1024 ** 3)
  editHostingScope.value = node.hostingScope ?? 0
}

async function loadPage() {
  loading.value = true
  error.value = null
  try {
    const orgResult = await api.organizations.getBySlug(orgSlug.value)
    if (!orgResult.data) {
      error.value = t('org.storage.notFound')
      return
    }
    organizationId.value = orgResult.data.id
    const [settingsResult, nodesResult, enrollmentsResult] = await Promise.all([
      api.organizations.storage.getSettings(orgResult.data.id),
      api.organizations.storage.listNodes(orgResult.data.id),
      api.organizations.storage.listEnrollments(orgResult.data.id),
    ])
    if (
      settingsResult.status === 403
      || nodesResult.status === 403
      || enrollmentsResult.status === 403
    ) {
      forbidden.value = true
      error.value = t('org.storage.ownerOnly')
      return
    }
    settings.value = settingsResult.data
    nodes.value = nodesResult.data ?? []
    enrollments.value = enrollmentsResult.data ?? []
    if (settingsResult.data) {
      placementPolicy.value = settingsResult.data.defaultPlacementPolicy
      selfHostPreference.value = settingsResult.data.defaultSelfHostPreference
    }
  }
  catch (e) {
    error.value = e instanceof Error ? e.message : t('org.storage.loadFailed')
  }
  finally {
    loading.value = false
  }
}

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
    error.value = e instanceof Error ? e.message : t('org.storage.saveFailed')
  }
  finally {
    saving.value = false
  }
}

async function createEnrollment() {
  if (!organizationId.value) return
  createLoading.value = true
  createdEnrollmentToken.value = null
  createdEnrollmentNodeId.value = null
  try {
    const result = await api.organizations.storage.createEnrollment(organizationId.value, {
      nodeId: createNodeId.value,
      maxBytes: gibiToBytes(createMaxGiB.value),
      hostingScope: createHostingScope.value,
    })
    if (result.error || !result.data?.enrollmentToken) {
      error.value = result.error ?? t('org.storage.createFailed')
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

async function saveNodeEdit(nodeId: string) {
  if (!organizationId.value) return
  editLoading.value = true
  error.value = null
  try {
    const capacityResult = await api.organizations.storage.updateCapacity(
      organizationId.value,
      nodeId,
      { maxBytes: gibiToBytes(editMaxGiB.value) },
    )
    if (capacityResult.error) {
      error.value = capacityResult.error
      return
    }
    const scopeResult = await api.organizations.storage.updateHostingScope(
      organizationId.value,
      nodeId,
      { hostingScope: editHostingScope.value },
    )
    if (scopeResult.error) {
      error.value = scopeResult.error
      return
    }
    editingNodeId.value = null
    await loadPage()
  }
  finally {
    editLoading.value = false
  }
}

function downloadBootstrapScript() {
  if (!createdEnrollmentToken.value || !createdEnrollmentNodeId.value) {
    return
  }
  const script = buildOrgStorageBootstrapDownloadScript({
    enrollmentToken: createdEnrollmentToken.value,
    nodeId: createdEnrollmentNodeId.value,
    apiUrl: apiBaseUrl.value,
    internalHost: bootstrapInternalHost.value,
  })
  const blob = new Blob([script], { type: 'text/x-shellscript' })
  const url = URL.createObjectURL(blob)
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = `bootstrap-${createdEnrollmentNodeId.value}.sh`
  anchor.click()
  URL.revokeObjectURL(url)
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
        {{ t('org.storage.title') }}
      </h1>
      <p class="mt-1 text-sm text-[var(--ogb-text-muted)]">
        {{ t('org.storage.description') }}
        <NuxtLink
          to="/docs/storage/org-storage-nodes"
          class="text-[var(--ogb-accent)] hover:underline"
        >
          {{ t('org.storage.docsLink') }}
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
      <OrgStorageQuotaCard
        v-if="settings"
        :settings="settings"
      />

      <OrgStorageNodeList
        :nodes="nodes"
        :editing-node-id="editingNodeId"
        :edit-max-gi-b="editMaxGiB"
        :edit-hosting-scope="editHostingScope"
        :edit-loading="editLoading"
        @start-edit="startEdit"
        @cancel-edit="editingNodeId = null"
        @update:edit-max-gi-b="editMaxGiB = $event"
        @update:edit-hosting-scope="editHostingScope = $event"
        @save-edit="saveNodeEdit"
      />

      <OrgStorageEnrollmentSection
        :enrollments="enrollments"
        :create-node-id="createNodeId"
        :create-max-gi-b="createMaxGiB"
        :create-hosting-scope="createHostingScope"
        :create-loading="createLoading"
        :bootstrap-command="bootstrapCommand"
        @update:create-node-id="createNodeId = $event"
        @update:create-max-gi-b="createMaxGiB = $event"
        @update:create-hosting-scope="createHostingScope = $event"
        @create-enrollment="createEnrollment"
        @download-bootstrap="downloadBootstrapScript"
      />

      <UFormField
        v-if="bootstrapCommand"
        :label="t('org.storage.internalHostLabel')"
        class="px-1"
      >
        <UInput
          v-model="bootstrapInternalHost"
          placeholder="storage.example.com"
        />
      </UFormField>

      <OrgStoragePlacementForm
        v-if="settings"
        :placement-policy="placementPolicy"
        :self-host-preference="selfHostPreference"
        :placement-options="placementOptions"
        :self-host-options="selfHostOptions"
        :saving="saving"
        @update:placement-policy="placementPolicy = $event"
        @update:self-host-preference="selfHostPreference = $event"
        @save="saveSettings"
      />
    </template>
  </div>
</template>
