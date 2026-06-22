<script setup lang="ts">
import type { RepositoryUsage } from '~/utils/api'
import {
  formatStorageBytes,
  formatUsagePercent,
  usagePercent,
} from '~/utils/storageUsage'

const props = defineProps<{
  usage: RepositoryUsage | null
  loading?: boolean
}>()

const { t } = useI18n()

const usagePercentValue = computed(() => {
  if (!props.usage) {
    return 0
  }
  return usagePercent(props.usage.bytesUsed, props.usage.bytesLimit)
})

const percentLabel = computed(() => {
  if (!props.usage) {
    return '0'
  }
  return formatUsagePercent(usagePercentValue.value, props.usage.bytesUsed)
})

const warningThreshold = computed(() => usagePercentValue.value >= 80)
const atLimit = computed(() => usagePercentValue.value >= 100)

function formatBytes(bytes: number): string {
  return formatStorageBytes(bytes)
}
</script>

<template>
  <UCard>
    <template #header>
      <h3 class="text-sm font-semibold">
        {{ t('repo.storage.title') }}
      </h3>
    </template>

    <div
      v-if="loading"
      class="text-sm text-[var(--ogb-text-muted)]"
    >
      {{ t('common.loading') }}
    </div>

    <div
      v-else-if="usage"
      class="space-y-3"
    >
      <div class="flex items-center justify-between text-sm">
        <span>{{ formatBytes(usage.bytesUsed) }} / {{ formatBytes(usage.bytesLimit) }}</span>
        <span class="text-[var(--ogb-text-muted)]">{{ percentLabel }}%</span>
      </div>

      <UProgress
        :model-value="usagePercentValue"
        :color="atLimit ? 'error' : warningThreshold ? 'warning' : 'primary'"
      />

      <UAlert
        v-if="warningThreshold && !atLimit"
        color="warning"
        variant="subtle"
        icon="i-lucide-triangle-alert"
        :title="t('repo.storage.warningTitle')"
        :description="t('repo.storage.warningDescription')"
      />

      <UAlert
        v-if="atLimit"
        color="error"
        variant="subtle"
        icon="i-lucide-ban"
        :title="t('repo.storage.limitTitle')"
        :description="t('repo.storage.limitDescription')"
      />
    </div>

    <p
      v-else
      class="text-sm text-[var(--ogb-text-muted)]"
    >
      {{ t('repo.storage.unavailable') }}
    </p>
  </UCard>
</template>
