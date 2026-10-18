<script setup lang="ts">
import { renderSVG } from 'uqr'
import type { TotpSetup } from '~/utils/twoFactor'
import { formatSetupKey, normalizeAuthenticatorCode } from '~/utils/twoFactor'

const props = defineProps<{
  setup: TotpSetup
  loading?: boolean
  error?: string | null
  initialCode?: string
}>()

const emit = defineEmits<{
  confirm: [code: string]
  cancel: []
}>()

const { t } = useI18n()
const code = ref(props.initialCode ?? '')

// Rendered here in the browser: the secret never goes to a QR service.
const qrSvg = computed(() => renderSVG(props.setup.otpauthUri, { border: 2 }))
const setupKey = computed(() => formatSetupKey(props.setup.secret))

function onSubmit() {
  emit('confirm', normalizeAuthenticatorCode(code.value))
}
</script>

<template>
  <form
    class="space-y-5"
    data-testid="totp-setup-panel"
    @submit.prevent="onSubmit"
  >
    <ol class="list-decimal space-y-1 pl-5 text-sm text-[var(--ogb-text-muted)]">
      <li>{{ t('settings.security.setup.stepInstall') }}</li>
      <li>{{ t('settings.security.setup.stepScan') }}</li>
      <li>{{ t('settings.security.setup.stepConfirm') }}</li>
    </ol>

    <div class="flex flex-col items-center gap-4 sm:flex-row sm:items-start">
      <!-- eslint-disable vue/no-v-html -- SVG generated locally from our own setup URI -->
      <div
        class="size-44 shrink-0 rounded-md bg-white p-1 [&>svg]:size-full"
        role="img"
        :aria-label="t('settings.security.setup.qrLabel')"
        v-html="qrSvg"
      />
      <!-- eslint-enable vue/no-v-html -->
      <div class="min-w-0 space-y-1 text-sm">
        <p class="text-[var(--ogb-text-muted)]">
          {{ t('settings.security.setup.manualHint') }}
        </p>
        <p
          class="break-all font-mono text-base tracking-wider"
          data-testid="totp-setup-key"
        >
          {{ setupKey }}
        </p>
      </div>
    </div>

    <UFormField
      :label="t('auth.twoFactor.codeLabel')"
      required
    >
      <UInput
        v-model="code"
        autocomplete="one-time-code"
        inputmode="numeric"
        maxlength="9"
        placeholder="123 456"
        class="w-full font-mono"
        required
      />
    </UFormField>

    <UAlert
      v-if="error"
      color="error"
      variant="subtle"
      :description="error"
    />

    <div class="flex justify-end gap-2">
      <UButton
        variant="ghost"
        @click="emit('cancel')"
      >
        {{ t('common.cancel') }}
      </UButton>
      <UButton
        type="submit"
        :loading="loading"
      >
        {{ t('settings.security.setup.confirm') }}
      </UButton>
    </div>
  </form>
</template>
