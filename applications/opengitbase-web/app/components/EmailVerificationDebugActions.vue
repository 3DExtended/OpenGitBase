<script setup lang="ts">
const { t } = useI18n()
const debug = useEmailVerificationDebug()
</script>

<template>
  <div
    v-if="debug.isAvailable"
    class="flex flex-wrap items-center gap-2"
  >
    <span class="text-xs font-medium uppercase tracking-wide text-[var(--ogb-text-muted)]">
      {{ t('verification.debug.label') }}
    </span>
    <UButton
      size="xs"
      color="neutral"
      variant="outline"
      :loading="debug.verifying"
      @click="debug.verifyNow"
    >
      {{ t('verification.debug.verifyNow') }}
    </UButton>
    <UButton
      size="xs"
      color="neutral"
      variant="outline"
      :loading="debug.loadingCode"
      @click="debug.showCode"
    >
      {{ t('verification.debug.showCode') }}
    </UButton>
    <p
      v-if="debug.error"
      class="w-full text-xs text-error"
    >
      {{ debug.error }}
    </p>
  </div>
</template>
