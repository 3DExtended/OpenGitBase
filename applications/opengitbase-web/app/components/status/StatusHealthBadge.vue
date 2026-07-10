<script setup lang="ts">
import type { PublicHealthStatus } from '~/utils/publicStatus'
import { healthStatusLabel } from '~/utils/publicStatus'

const props = defineProps<{
  status: PublicHealthStatus
  label?: string
}>()

const { t } = useI18n()

const tone = computed(() => healthStatusLabel(props.status))

const color = computed(() => {
  switch (tone.value) {
    case 'healthy': return 'success'
    case 'degraded': return 'warning'
    default: return 'error'
  }
})

const text = computed(() => props.label ?? t(`status.health.${tone.value}`))
</script>

<template>
  <UBadge
    :color="color"
    variant="subtle"
    size="md"
  >
    {{ text }}
  </UBadge>
</template>
