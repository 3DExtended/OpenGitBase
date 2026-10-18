<script setup lang="ts">
defineProps<{
  loading: boolean
  /** A translation key. */
  error: string | null
}>()

defineEmits<{
  click: []
}>()

const { t } = useI18n()
</script>

<template>
  <div
    class="space-y-4"
    data-testid="passkey-sign-in-section"
  >
    <USeparator :label="t('auth.signIn.or')" />
    <UButton
      block
      color="neutral"
      variant="outline"
      icon="i-lucide-key-round"
      :loading="loading"
      data-testid="passkey-sign-in"
      @click="$emit('click')"
    >
      {{ t('auth.signIn.passkey.button') }}
    </UButton>
    <UAlert
      v-if="error"
      color="error"
      variant="subtle"
      :description="t(error)"
      data-testid="passkey-sign-in-error"
    />
  </div>
</template>
