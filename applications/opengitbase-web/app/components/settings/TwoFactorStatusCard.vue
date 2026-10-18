<script setup lang="ts">
import type { AccountSecurity } from '~/utils/twoFactor'

defineProps<{
  status: AccountSecurity
  busy?: boolean
}>()

const emit = defineEmits<{
  setup: []
  disable: []
}>()

const { t } = useI18n()
</script>

<template>
  <UCard data-testid="two-factor-status-card">
    <template #header>
      <div class="flex items-center justify-between gap-3">
        <h2 class="font-semibold">
          {{ t('settings.security.twoFactor.title') }}
        </h2>
        <UBadge
          :color="status.twoFactorEnabled ? 'success' : 'neutral'"
          variant="subtle"
        >
          {{ status.twoFactorEnabled ? t('settings.security.twoFactor.on') : t('settings.security.twoFactor.off') }}
        </UBadge>
      </div>
    </template>

    <p class="text-sm text-[var(--ogb-text-muted)]">
      {{ status.twoFactorEnabled ? t('settings.security.twoFactor.onDescription') : t('settings.security.twoFactor.offDescription') }}
    </p>

    <div
      v-if="status.totpConfigured"
      class="mt-4 flex items-center gap-2 text-sm"
    >
      <UIcon
        name="i-lucide-smartphone"
        class="size-4 text-[var(--ogb-accent)]"
      />
      <span>{{ t('settings.security.twoFactor.authenticatorApp') }}</span>
    </div>

    <template #footer>
      <div class="flex flex-wrap gap-2">
        <UButton
          v-if="status.twoFactorAvailable"
          :variant="status.twoFactorEnabled ? 'soft' : 'solid'"
          :loading="busy"
          icon="i-lucide-qr-code"
          @click="emit('setup')"
        >
          {{ status.totpConfigured ? t('settings.security.twoFactor.replace') : t('settings.security.twoFactor.enable') }}
        </UButton>
        <UButton
          v-if="status.twoFactorEnabled"
          color="error"
          variant="ghost"
          :disabled="busy"
          @click="emit('disable')"
        >
          {{ t('settings.security.twoFactor.disable') }}
        </UButton>
      </div>
    </template>
  </UCard>
</template>
