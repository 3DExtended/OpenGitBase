<script setup lang="ts">
import type { AdminRepositoryReplicationDetailDto } from '~/utils/api'

definePageMeta({ middleware: 'admin' })

const route = useRoute()
const { t } = useI18n()
const api = useApi()

const repositoryId = computed(() => String(route.params.id ?? ''))

useHead({ title: t('admin.replication.detailTitle') })

const loading = ref(true)
const error = ref<string | null>(null)
const detail = ref<AdminRepositoryReplicationDetailDto | null>(null)

const primaryReplica = computed(() =>
  detail.value?.replicas.find(replica => replica.role === 'Primary') ?? null,
)
const secondaryReplicas = computed(() =>
  detail.value?.replicas.filter(replica => replica.role !== 'Primary') ?? [],
)

async function loadDetail() {
  loading.value = true
  error.value = null
  try {
    const result = await api.admin.replication.getRepository(repositoryId.value)
    if (result.status === 404) {
      detail.value = null
      error.value = t('admin.replication.detail.notFound')
      return
    }
    if (result.error || !result.data) {
      error.value = result.error ?? t('admin.replication.detail.notFound')
      return
    }
    detail.value = result.data
  }
  catch (e) {
    error.value = e instanceof Error ? e.message : t('admin.replication.detail.notFound')
  }
  finally {
    loading.value = false
  }
}

onMounted(loadDetail)
useAdminReplicationPoll(loadDetail)
</script>

<template>
  <div class="mx-auto max-w-5xl space-y-6">
    <UButton
      to="/admin/repositories"
      variant="ghost"
      size="sm"
      icon="i-lucide-arrow-left"
      class="-ml-2"
    >
      {{ t('admin.replication.indexTitle') }}
    </UButton>

    <UAlert
      v-if="error"
      color="error"
      variant="subtle"
      :description="error"
    />

    <div
      v-if="loading && !detail"
      class="text-sm text-[var(--ogb-text-muted)]"
    >
      {{ t('common.loading') }}
    </div>

    <template v-else-if="detail">
      <div class="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h1 class="text-2xl font-semibold">
            {{ detail.name }}
          </h1>
          <p class="mt-1 text-sm text-[var(--ogb-text-muted)]">
            {{ detail.ownerSlug }}
          </p>
          <UButton
            :to="`/${detail.ownerSlug}/${detail.slug}`"
            variant="link"
            class="mt-2 px-0"
            trailing-icon="i-lucide-external-link"
          >
            {{ t('admin.replication.detail.publicRepo') }}
          </UButton>
        </div>
        <UButton
          icon="i-lucide-refresh-cw"
          variant="soft"
          :loading="loading"
          @click="loadDetail"
        >
          {{ t('admin.replication.refresh') }}
        </UButton>
      </div>

      <UCard>
        <div class="flex flex-wrap items-center gap-3">
          <AdminReplicationStateBadge :state="detail.replicationState" />
          <UBadge
            :color="detail.writeQuorumAvailable ? 'success' : 'error'"
            variant="subtle"
          >
            {{ t('admin.replication.detail.writeQuorum') }}:
            {{ detail.writeQuorumAvailable ? t('admin.replication.quorum.yes') : t('admin.replication.quorum.no') }}
          </UBadge>
          <span class="text-sm text-[var(--ogb-text-muted)]">
            {{ t('admin.replication.detail.primaryWatermark') }}: {{ detail.primaryWatermark }}
          </span>
          <span class="text-sm text-[var(--ogb-text-muted)]">
            {{ t('admin.replication.columns.epoch') }}: {{ detail.replicationEpoch }}
          </span>
        </div>
      </UCard>

      <UCard>
        <template #header>
          <h2 class="font-semibold">
            {{ t('admin.replication.detail.replicasTitle') }}
          </h2>
        </template>

        <div class="grid gap-4 md:grid-cols-3">
          <UCard
            v-if="primaryReplica"
            class="border-[var(--ogb-accent)] bg-[var(--ogb-bg)] md:col-span-1"
          >
            <div class="space-y-2">
              <UBadge color="primary" variant="subtle">
                {{ t('admin.replication.detail.primaryNode') }}
              </UBadge>
              <div class="font-medium">
                {{ primaryReplica.nodeId || primaryReplica.storageNodeId }}
              </div>
              <div class="text-xs text-[var(--ogb-text-muted)]">
                {{ t('admin.replication.detail.appliedWatermark') }}: {{ primaryReplica.appliedWatermark }}
              </div>
              <div class="text-xs text-[var(--ogb-text-muted)]">
                {{ t('admin.replication.detail.lag') }}:
                {{ Math.max(0, detail.primaryWatermark - primaryReplica.appliedWatermark) }}
              </div>
              <UBadge
                :color="primaryReplica.isInSync ? 'success' : 'warning'"
                variant="subtle"
              >
                {{ primaryReplica.isInSync ? t('admin.replication.detail.inSync') : t('admin.replication.filters.lagging') }}
              </UBadge>
            </div>
          </UCard>

          <div class="space-y-3 md:col-span-2">
            <UCard
              v-for="replica in secondaryReplicas"
              :key="replica.storageNodeId"
              class="bg-[var(--ogb-bg)]"
            >
              <div class="flex flex-wrap items-start justify-between gap-3">
                <div>
                  <UBadge color="neutral" variant="subtle">
                    {{ t('admin.replication.detail.replicaNode') }}
                  </UBadge>
                  <div class="mt-1 font-medium">
                    {{ replica.nodeId || replica.storageNodeId }}
                  </div>
                </div>
                <UBadge
                  :color="replica.isInSync ? 'success' : 'warning'"
                  variant="subtle"
                >
                  {{ replica.isInSync ? t('admin.replication.detail.inSync') : t('admin.replication.filters.lagging') }}
                </UBadge>
              </div>
              <div class="mt-2 grid gap-1 text-xs text-[var(--ogb-text-muted)] sm:grid-cols-3">
                <span>{{ t('admin.replication.detail.appliedWatermark') }}: {{ replica.appliedWatermark }}</span>
                <span>{{ t('admin.replication.detail.lag') }}: {{ Math.max(0, detail.primaryWatermark - replica.appliedWatermark) }}</span>
                <span>{{ t('admin.replication.detail.lastSynced') }}: {{ replica.lastSyncedAt ?? '—' }}</span>
              </div>
            </UCard>
          </div>
        </div>
      </UCard>
    </template>
  </div>
</template>
