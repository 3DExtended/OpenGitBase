<script setup lang="ts">
import { replicationStateBadgeColor } from '~/composables/useAdminReplication'

const props = defineProps<{
  state: string
}>()

const { t } = useI18n()

const label = computed(() => t(`admin.replication.states.${props.state}`, props.state))
</script>

<template>
  <UBadge
    :color="replicationStateBadgeColor(props.state)"
    variant="subtle"
  >
    {{ label }}
  </UBadge>
</template>
