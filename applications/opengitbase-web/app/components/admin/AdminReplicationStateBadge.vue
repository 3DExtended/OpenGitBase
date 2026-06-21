<script setup lang="ts">
import { replicationStateBadgeColor } from '~/composables/useAdminReplication'

defineProps<{
  state: string
}>()

const { t } = useI18n()

const label = computed(() => t(`admin.replication.states.${state}`, state))
</script>

<template>
  <UBadge
    :color="replicationStateBadgeColor(state)"
    variant="subtle"
  >
    {{ label }}
  </UBadge>
</template>
