<script setup lang="ts">
import type { RepositoryUsage } from '~/utils/api'

const props = defineProps<{
  usage: RepositoryUsage | null
  loading?: boolean
}>()

const { t } = useI18n()

const percentUsed = computed(() => {
  if (!props.usage || props.usage.bytesLimit <= 0) {
    return 0
  }
  return Math.min(100, Math.round((props.usage.bytesUsed / props.usage.bytesLimit) * 100))
})

const warningThreshold = computed(() => percentUsed.value >= 80)
const atLimit = computed(() => percentUsed.value >= 100)

function formatBytes(bytes: number): string {
  if (bytes >= 1_073_741_824) {
    return `${(bytes / 1_073_741_824).toFixed(2)} GB`
  }
  if (bytes >= 1_048_576) {
    return `${(bytes / 1_048_576).toFixed(1)} MB`
  }
  return `${(bytes / 1024).toFixed(0)} KB`
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
        <span class="text-[var(--ogb-text-muted)]">{{ percentUsed }}%</span>
      </div>

      <UProgress
        :model-value="percentUsed"
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
