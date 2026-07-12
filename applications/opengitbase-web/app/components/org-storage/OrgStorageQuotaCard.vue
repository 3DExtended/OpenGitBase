<script setup lang="ts">
import type { OrganizationStorageSettings } from '~/utils/api'
import { formatStorageBytes } from '~/utils/storageUsage'

defineProps<{
  settings: OrganizationStorageSettings
}>()

const { t } = useI18n()

function formatBytes(value: number) {
  if (value <= 0) return t('org.storage.unlimited')
  return formatStorageBytes(value)
}
</script>

<template>
  <UCard>
    <template #header>
      <h2 class="font-semibold">
        {{ t('org.storage.quotaTitle') }}
      </h2>
    </template>
    <dl class="grid gap-3 text-sm sm:grid-cols-3">
      <div>
        <dt class="text-[var(--ogb-text-muted)]">
          {{ t('org.storage.platformLimit') }}
        </dt>
        <dd>{{ formatBytes(settings.platformBytesLimit) }}</dd>
      </div>
      <div>
        <dt class="text-[var(--ogb-text-muted)]">
          {{ t('org.storage.contributedCapacity') }}
        </dt>
        <dd>{{ formatBytes(settings.contributedBytesCapacity) }}</dd>
      </div>
      <div>
        <dt class="text-[var(--ogb-text-muted)]">
          {{ t('org.storage.effectiveLimit') }}
        </dt>
        <dd>{{ formatBytes(settings.bytesLimit) }}</dd>
      </div>
    </dl>
  </UCard>
</template>
