<script setup lang="ts">
import type { RepositoryReplicationLag } from '~/utils/api'

defineProps<{
  lag?: RepositoryReplicationLag | null
}>()

const { t } = useI18n()
</script>

<template>
  <div
    v-if="lag?.behind"
    class="rounded-md border border-amber-500/30 bg-amber-500/10 px-4 py-2 text-sm text-[var(--ogb-text)]"
    role="status"
  >
    {{ lag.message ?? t('repo.browse.syncing') }}
  </div>
</template>
