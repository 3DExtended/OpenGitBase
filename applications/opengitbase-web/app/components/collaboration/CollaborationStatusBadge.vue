<script setup lang="ts">
withDefaults(
  defineProps<{
    label: string
    color?: 'neutral' | 'info' | 'success' | 'warning' | 'error'
    size?: 'xs' | 'sm' | 'md'
  }>(),
  {
    color: 'neutral',
    size: 'sm',
  },
)
</script>

<template>
  <UBadge
    :color="color"
    variant="subtle"
    :size="size"
  >
    {{ label }}
  </UBadge>
</template>
