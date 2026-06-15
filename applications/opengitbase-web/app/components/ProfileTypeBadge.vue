<script setup lang="ts">
defineProps<{
  kind: 'user' | 'organization'
}>()

const { t } = useI18n()
</script>

<template>
  <UBadge
    :color="kind === 'organization' ? 'primary' : 'neutral'"
    variant="subtle"
    size="xs"
  >
    {{ kind === 'organization' ? t('profile.org') : t('profile.user') }}
  </UBadge>
</template>
