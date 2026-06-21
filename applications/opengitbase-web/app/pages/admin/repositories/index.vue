<script setup lang="ts">
import type { AdminRepositoryReplicationSummaryDto } from '~/utils/api'

definePageMeta({ middleware: 'admin' })

const { t } = useI18n()
const api = useApi()

useHead({ title: t('admin.replication.indexTitle') })

const loading = ref(true)
const error = ref<string | null>(null)
const items = ref<AdminRepositoryReplicationSummaryDto[]>([])
const totalCount = ref(0)
const page = ref(1)
const pageSize = ref(50)
const sort = ref('severity')
const search = ref('')
const attention = ref('all')

const attentionOptions = [
  { label: t('admin.replication.filters.all'), value: 'all' },
  { label: t('admin.replication.filters.backfilling'), value: 'backfilling' },
  { label: t('admin.replication.filters.degraded'), value: 'degraded' },
  { label: t('admin.replication.filters.lagging'), value: 'lagging' },
  { label: t('admin.replication.filters.noQuorum'), value: 'no-quorum' },
]

const sortOptions = [
  { label: t('admin.replication.sort.severity'), value: 'severity' },
  { label: t('admin.replication.sort.name'), value: 'name' },
  { label: t('admin.replication.sort.lag'), value: 'lag' },
  { label: t('admin.replication.sort.state'), value: 'state' },
]

const route = useRoute()
const router = useRouter()

async function loadRepositories() {
  loading.value = true
  error.value = null
  try {
    const result = await api.admin.replication.listRepositories({
      page: page.value,
      pageSize: pageSize.value,
      sort: sort.value,
      search: search.value || undefined,
      attention: attention.value === 'all' ? undefined : attention.value,
    })
    if (result.error) {
      error.value = result.error
      return
    }
    items.value = result.data?.items ?? []
    totalCount.value = result.data?.totalCount ?? 0
  }
  catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to load repositories.'
  }
  finally {
    loading.value = false
  }
}

function applyRouteQuery() {
  page.value = Number(route.query.page ?? 1) || 1
  attention.value = String(route.query.attention ?? 'all')
  search.value = String(route.query.search ?? '')
  sort.value = String(route.query.sort ?? 'severity')
}

async function syncQueryAndLoad() {
  await router.replace({
    query: {
      page: page.value > 1 ? String(page.value) : undefined,
      attention: attention.value !== 'all' ? attention.value : undefined,
      search: search.value || undefined,
      sort: sort.value !== 'severity' ? sort.value : undefined,
    },
  })
  await loadRepositories()
}

watch([page, sort, attention], () => {
  void syncQueryAndLoad()
})

let searchTimer: ReturnType<typeof setTimeout> | undefined
watch(search, () => {
  clearTimeout(searchTimer)
  searchTimer = setTimeout(() => {
    page.value = 1
    void syncQueryAndLoad()
  }, 300)
})

onMounted(() => {
  applyRouteQuery()
  void loadRepositories()
})

useAdminReplicationPoll(loadRepositories)
</script>

<template>
  <div class="mx-auto max-w-6xl space-y-6">
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
          {{ t('admin.replication.indexTitle') }}
        </h1>
        <p class="mt-1 text-sm text-[var(--ogb-text-muted)]">
          {{ t('admin.replication.indexDescription') }}
        </p>
      </div>
      <UButton
        icon="i-lucide-refresh-cw"
        variant="soft"
        :loading="loading"
        @click="loadRepositories"
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

    <UCard>
      <div class="flex flex-wrap gap-3">
        <UInput
          v-model="search"
          :placeholder="t('admin.replication.searchPlaceholder')"
          icon="i-lucide-search"
          class="min-w-[16rem] flex-1"
        />
        <USelect
          v-model="attention"
          :items="attentionOptions"
          class="min-w-[10rem]"
        />
        <USelect
          v-model="sort"
          :items="sortOptions"
          class="min-w-[10rem]"
        />
      </div>
    </UCard>

    <UCard>
      <div
        v-if="loading && !items.length"
        class="text-sm text-[var(--ogb-text-muted)]"
      >
        {{ t('common.loading') }}
      </div>
      <div
        v-else-if="!items.length"
        class="text-sm text-[var(--ogb-text-muted)]"
      >
        {{ t('admin.replication.empty') }}
      </div>
      <div
        v-else
        class="overflow-x-auto"
      >
        <table class="min-w-full text-sm">
          <thead>
            <tr class="border-b border-[var(--ogb-border)] text-left text-[var(--ogb-text-muted)]">
              <th class="px-2 py-2">
                {{ t('admin.replication.columns.name') }}
              </th>
              <th class="px-2 py-2">
                {{ t('admin.replication.columns.owner') }}
              </th>
              <th class="px-2 py-2">
                {{ t('admin.replication.columns.state') }}
              </th>
              <th class="px-2 py-2">
                {{ t('admin.replication.columns.replicas') }}
              </th>
              <th class="px-2 py-2">
                {{ t('admin.replication.columns.primary') }}
              </th>
              <th class="px-2 py-2">
                {{ t('admin.replication.columns.lag') }}
              </th>
              <th class="px-2 py-2">
                {{ t('admin.replication.columns.quorum') }}
              </th>
            </tr>
          </thead>
          <tbody>
            <tr
              v-for="item in items"
              :key="item.repositoryId"
              class="cursor-pointer border-b border-[var(--ogb-border)] hover:bg-[var(--ogb-surface-muted)]"
              @click="navigateTo(`/admin/repositories/${item.repositoryId}`)"
            >
              <td class="px-2 py-3 font-medium">
                {{ item.name }}
              </td>
              <td class="px-2 py-3">
                {{ item.ownerSlug }}
              </td>
              <td class="px-2 py-3">
                <AdminReplicationStateBadge :state="item.replicationState" />
              </td>
              <td class="px-2 py-3">
                <AdminReplicationProgressBars
                  :replica-count="item.replicaCount"
                  :max-watermark-lag="item.maxWatermarkLag"
                  :primary-watermark="item.primaryWatermark"
                  compact
                />
              </td>
              <td class="px-2 py-3">
                {{ item.primaryNodeId || '—' }}
              </td>
              <td class="px-2 py-3">
                {{ item.maxWatermarkLag }}
              </td>
              <td class="px-2 py-3">
                <UBadge
                  :color="item.writeQuorumAvailable ? 'success' : 'error'"
                  variant="subtle"
                >
                  {{ item.writeQuorumAvailable ? t('admin.replication.quorum.yes') : t('admin.replication.quorum.no') }}
                </UBadge>
              </td>
            </tr>
          </tbody>
        </table>
      </div>

      <div
        v-if="totalCount > pageSize"
        class="mt-4 flex items-center justify-between gap-3"
      >
        <span class="text-sm text-[var(--ogb-text-muted)]">{{ totalCount }} repositories</span>
        <div class="flex gap-2">
          <UButton
            variant="soft"
            size="sm"
            :disabled="page <= 1"
            @click="page--"
          >
            Previous
          </UButton>
          <UButton
            variant="soft"
            size="sm"
            :disabled="page * pageSize >= totalCount"
            @click="page++"
          >
            Next
          </UButton>
        </div>
      </div>
    </UCard>
  </div>
</template>
