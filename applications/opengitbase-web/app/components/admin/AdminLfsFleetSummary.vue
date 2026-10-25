<script setup lang="ts">
import type { AdminLfsFleetSummaryDto } from '~/utils/api'
import { formatStorageBytes } from '~/utils/storageUsage'

defineProps<{
  summary: AdminLfsFleetSummaryDto
}>()

const { t } = useI18n()
</script>

<template>
  <UCard data-testid="admin-lfs-fleet-summary">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <div>
        <h2 class="text-sm font-semibold">
          {{ t('admin.replication.lfs.fleetTitle') }}
        </h2>
        <p class="mt-1 text-sm text-[var(--ogb-text-muted)]">
          {{ t('admin.replication.lfs.objects', { count: summary.objectCount }) }}
          ·
          {{ t('admin.replication.lfs.stored', { bytes: formatStorageBytes(summary.totalBytes) }) }}
        </p>
      </div>
      <UBadge
        :color="summary.missingCopies > 0 ? 'warning' : 'success'"
        variant="subtle"
      >
        {{
          summary.missingCopies > 0
            ? t('admin.replication.lfs.fleetMissing', { count: summary.missingCopies, repositories: summary.repositoriesWithMissingCopies })
            : t('admin.replication.lfs.complete')
        }}
      </UBadge>
    </div>
  </UCard>
</template>
