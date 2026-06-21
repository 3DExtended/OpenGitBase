<script setup lang="ts">
import {
  provisioningProgressPercent,
  syncProgressPercent,
} from '~/composables/useAdminReplication'

defineProps<{
  replicaCount: number
  maxWatermarkLag: number
  primaryWatermark: number
  compact?: boolean
}>()

const { t } = useI18n()
</script>

<template>
  <div :class="compact ? 'space-y-1' : 'space-y-2'">
    <div>
      <div class="mb-1 flex justify-between text-xs text-[var(--ogb-text-muted)]">
        <span>{{ t('admin.replication.progress.provisioning') }}</span>
        <span>{{ replicaCount }}/3</span>
      </div>
      <UProgress :model-value="provisioningProgressPercent(replicaCount)" color="primary" size="sm" />
    </div>
    <div>
      <div class="mb-1 flex justify-between text-xs text-[var(--ogb-text-muted)]">
        <span>{{ t('admin.replication.progress.sync') }}</span>
        <span>{{ syncProgressPercent(maxWatermarkLag, primaryWatermark) }}%</span>
      </div>
      <UProgress
        :model-value="syncProgressPercent(maxWatermarkLag, primaryWatermark)"
        :color="maxWatermarkLag > 0 ? 'warning' : 'success'"
        size="sm"
      />
    </div>
  </div>
</template>
