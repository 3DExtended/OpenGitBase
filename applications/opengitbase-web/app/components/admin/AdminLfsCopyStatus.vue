<script setup lang="ts">
import type { AdminRepositoryLfsStatusDto } from '~/utils/api'
import { lfsCopyPercent, lfsRestoreProgress } from '~/utils/adminLfs'
import { formatStorageBytes } from '~/utils/storageUsage'

const props = defineProps<{
  lfs: AdminRepositoryLfsStatusDto
}>()

const { t } = useI18n()

const restore = computed(() => lfsRestoreProgress(props.lfs))
</script>

<template>
  <UCard data-testid="admin-lfs-copy-status">
    <template #header>
      <div class="flex flex-wrap items-center justify-between gap-2">
        <h2 class="font-semibold">
          {{ t('admin.replication.lfs.title') }}
        </h2>
        <div class="flex flex-wrap items-center gap-2 text-sm text-[var(--ogb-text-muted)]">
          <span>{{ t('admin.replication.lfs.objects', { count: lfs.objectCount }) }}</span>
          <span>·</span>
          <span>{{ t('admin.replication.lfs.stored', { bytes: formatStorageBytes(lfs.totalBytes) }) }}</span>
          <UBadge
            :color="lfs.missingCopies > 0 ? 'warning' : 'success'"
            variant="subtle"
            data-testid="admin-lfs-missing"
          >
            {{ lfs.missingCopies > 0 ? t('admin.replication.lfs.missingCopies', { count: lfs.missingCopies }) : t('admin.replication.lfs.complete') }}
          </UBadge>
        </div>
      </div>
    </template>

    <UAlert
      v-if="restore"
      color="warning"
      variant="subtle"
      icon="i-lucide-rotate-ccw"
      class="mb-4"
      data-testid="admin-lfs-restore"
      :description="t('admin.replication.lfs.restoring', { node: restore.nodeId, restored: restore.restored, total: restore.total })"
    />

    <ul class="space-y-3">
      <li
        v-for="node in lfs.nodes"
        :key="node.storageNodeId"
        class="space-y-1"
      >
        <div class="flex flex-wrap items-center justify-between gap-2 text-sm">
          <div class="flex flex-wrap items-center gap-2">
            <span class="font-medium">{{ node.nodeId || node.storageNodeId }}</span>
            <UBadge
              v-if="node.isPrimary"
              color="primary"
              variant="subtle"
              size="sm"
            >
              {{ t('admin.replication.lfs.primary') }}
            </UBadge>
            <UBadge
              color="neutral"
              variant="subtle"
              size="sm"
            >
              {{ node.kind === 'Encrypted' ? t('admin.replication.lfs.encrypted') : t('admin.replication.lfs.plaintext') }}
            </UBadge>
          </div>
          <div class="flex items-center gap-2 text-xs text-[var(--ogb-text-muted)]">
            <span>{{ t('admin.replication.lfs.copies', { copies: node.copies, total: lfs.objectCount }) }}</span>
            <UBadge
              v-if="node.missing > 0"
              color="warning"
              variant="subtle"
              size="sm"
            >
              {{ t('admin.replication.lfs.missing', { count: node.missing }) }}
            </UBadge>
          </div>
        </div>
        <UProgress
          :model-value="lfsCopyPercent(node.copies, lfs.objectCount)"
          :color="node.missing > 0 ? 'warning' : 'success'"
          size="sm"
        />
      </li>
    </ul>
  </UCard>
</template>
