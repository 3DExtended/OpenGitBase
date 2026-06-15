<script setup lang="ts">
const { t } = useI18n()
const debug = useEmailVerificationDebug()
</script>

<template>
  <div
    v-if="debug.isAvailable"
    class="mt-4 rounded-lg border border-dashed border-warning/50 bg-warning/5 p-4"
  >
    <p class="text-xs font-medium uppercase tracking-wide text-warning">
      {{ t('verification.debug.label') }}
    </p>
    <p class="mt-1 text-sm text-[var(--ogb-text-muted)]">
      {{ t('verification.debug.settingsDescription') }}
    </p>

    <div class="mt-3 flex flex-wrap gap-2">
      <UButton
        size="sm"
        color="warning"
        variant="soft"
        :loading="debug.verifying"
        @click="debug.verifyNow"
      >
        {{ t('verification.debug.verifyNow') }}
      </UButton>
      <UButton
        size="sm"
        color="warning"
        variant="outline"
        :loading="debug.loadingCode"
        @click="debug.showCode"
      >
        {{ t('verification.debug.showCode') }}
      </UButton>
    </div>

    <div
      v-if="debug.code"
      class="mt-4 space-y-2"
    >
      <p class="text-xs text-[var(--ogb-text-muted)]">
        {{ t('verification.debug.codeLabel') }}
      </p>
      <div class="flex flex-wrap items-center gap-2">
        <code class="rounded bg-[var(--ogb-surface)] px-2 py-1 font-mono text-sm">
          {{ debug.code }}
        </code>
        <UButton
          size="xs"
          variant="ghost"
          @click="debug.copyCode"
        >
          {{ t('verification.debug.copyCode') }}
        </UButton>
        <UButton
          v-if="debug.verifyUrl"
          size="xs"
          variant="soft"
          :to="debug.verifyUrl"
        >
          {{ t('verification.debug.openVerifyPage') }}
        </UButton>
      </div>
      <p
        v-if="debug.expiresAt"
        class="text-xs text-[var(--ogb-text-muted)]"
      >
        {{ t('verification.debug.expiresAt', { date: new Date(debug.expiresAt).toLocaleString() }) }}
      </p>
    </div>

    <p
      v-if="debug.error"
      class="mt-2 text-xs text-error"
    >
      {{ debug.error }}
    </p>
  </div>
</template>
