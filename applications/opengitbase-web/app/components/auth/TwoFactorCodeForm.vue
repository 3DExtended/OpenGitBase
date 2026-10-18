<script setup lang="ts">
import type { SecondFactorAnswer } from '~/utils/twoFactor'
import { normalizeAuthenticatorCode } from '~/utils/twoFactor'

const props = defineProps<{
  loading?: boolean
  error?: string | null
  /** The challenge offers `recovery`: the account still has unused recovery codes. */
  recoveryAvailable?: boolean
  /** Pre-fills the field; only the visual gallery uses it. */
  initialCode?: string
  /** Opens with the recovery-code field; only the visual gallery uses it. */
  initialRecovery?: boolean
}>()

const emit = defineEmits<{
  submit: [answer: SecondFactorAnswer]
  /** The user switched between authenticator code and recovery code; a shown error no longer applies. */
  switchMethod: []
  back: []
}>()

const { t } = useI18n()
const useRecovery = ref(props.initialRecovery ?? false)
const code = ref(props.initialCode ?? '')

function toggleMethod() {
  useRecovery.value = !useRecovery.value
  code.value = ''
  emit('switchMethod')
}

function onSubmit() {
  emit('submit', useRecovery.value
    ? { recoveryCode: code.value.trim() }
    : { code: normalizeAuthenticatorCode(code.value) })
}
</script>

<template>
  <form
    class="space-y-4"
    data-testid="two-factor-code-form"
    @submit.prevent="onSubmit"
  >
    <p class="text-sm text-[var(--ogb-text-muted)]">
      {{ useRecovery ? t('auth.twoFactor.recoveryInstructions') : t('auth.twoFactor.instructions') }}
    </p>

    <UFormField
      v-if="useRecovery"
      :label="t('auth.twoFactor.recoveryCodeLabel')"
      required
    >
      <UInput
        v-model="code"
        autocomplete="off"
        autocapitalize="off"
        :spellcheck="false"
        maxlength="20"
        placeholder="xxxxx-xxxxx"
        class="w-full font-mono"
        data-testid="recovery-code-input"
        autofocus
        required
      />
    </UFormField>

    <UFormField
      v-else
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
        autofocus
        required
      />
    </UFormField>

    <UAlert
      v-if="error"
      color="error"
      variant="subtle"
      :description="error"
    />

    <UButton
      type="submit"
      block
      :loading="loading"
    >
      {{ t('auth.twoFactor.submit') }}
    </UButton>

    <div class="flex flex-wrap items-center justify-between gap-2">
      <UButton
        variant="link"
        size="sm"
        class="px-0"
        icon="i-lucide-arrow-left"
        @click="emit('back')"
      >
        {{ t('auth.twoFactor.back') }}
      </UButton>
      <UButton
        v-if="recoveryAvailable || useRecovery"
        variant="link"
        size="sm"
        class="px-0"
        :icon="useRecovery ? 'i-lucide-smartphone' : 'i-lucide-life-buoy'"
        data-testid="two-factor-toggle-method"
        @click="toggleMethod"
      >
        {{ useRecovery ? t('auth.twoFactor.useAuthenticatorApp') : t('auth.twoFactor.useRecoveryCode') }}
      </UButton>
    </div>
  </form>
</template>
