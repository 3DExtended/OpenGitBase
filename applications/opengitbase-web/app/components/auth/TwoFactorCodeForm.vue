<script setup lang="ts">
import type { SecondFactorAnswer } from '~/utils/twoFactor'
import { normalizeAuthenticatorCode } from '~/utils/twoFactor'

const props = withDefaults(defineProps<{
  loading?: boolean
  error?: string | null
  /** The challenge offers `recovery`: the account still has unused recovery codes. */
  recoveryAvailable?: boolean
  /** The challenge offers `totp`; passkey-only accounts have no authenticator app. */
  codeAvailable?: boolean
  /** The challenge offers `passkey` and the browser can use one. */
  passkeyAvailable?: boolean
  /** Pre-fills the field; only the visual gallery uses it. */
  initialCode?: string
  /** Opens with the recovery-code field; only the visual gallery uses it. */
  initialRecovery?: boolean
}>(), { error: null, codeAvailable: true, initialCode: '' })

const emit = defineEmits<{
  submit: [answer: SecondFactorAnswer]
  /** The user chose to answer with a passkey; the page runs the browser prompt. */
  passkey: []
  /** The user switched between authenticator code and recovery code; a shown error no longer applies. */
  switchMethod: []
  back: []
}>()

type Mode = 'code' | 'recovery' | 'passkey'

const { t } = useI18n()
/** Without an authenticator app the passkey is the main way; without either, only a recovery code is left. */
const answerMode: Mode = props.codeAvailable ? 'code' : 'passkey'
const mode = ref<Mode>(props.initialRecovery || (!props.codeAvailable && !props.passkeyAvailable) ? 'recovery' : answerMode)
const code = ref(props.initialCode ?? '')
const canLeaveRecovery = computed(() => props.codeAvailable || props.passkeyAvailable)

function toggleMethod() {
  mode.value = mode.value === 'recovery' ? answerMode : 'recovery'
  code.value = ''
  emit('switchMethod')
}

function onSubmit() {
  if (mode.value === 'passkey') {
    emit('passkey')
    return
  }
  emit('submit', mode.value === 'recovery'
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
      {{
        mode === 'recovery'
          ? t('auth.twoFactor.recoveryInstructions')
          : mode === 'passkey' ? t('auth.twoFactor.passkeyInstructions') : t('auth.twoFactor.instructions')
      }}
    </p>

    <template v-if="passkeyAvailable && mode !== 'recovery'">
      <UButton
        block
        :variant="mode === 'passkey' ? 'solid' : 'soft'"
        icon="i-lucide-fingerprint"
        :loading="loading && mode === 'passkey'"
        :disabled="loading"
        data-testid="two-factor-use-passkey"
        @click="emit('passkey')"
      >
        {{ t('auth.twoFactor.usePasskey') }}
      </UButton>
      <USeparator
        v-if="mode === 'code'"
        :label="t('auth.signIn.or')"
      />
    </template>

    <UFormField
      v-if="mode === 'recovery'"
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
      v-else-if="mode === 'code'"
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
      data-testid="two-factor-error"
    />

    <UButton
      v-if="mode !== 'passkey'"
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
        v-if="mode === 'recovery' ? canLeaveRecovery : recoveryAvailable"
        variant="link"
        size="sm"
        class="px-0"
        :icon="mode !== 'recovery' ? 'i-lucide-life-buoy' : codeAvailable ? 'i-lucide-smartphone' : 'i-lucide-fingerprint'"
        data-testid="two-factor-toggle-method"
        @click="toggleMethod"
      >
        {{
          mode !== 'recovery'
            ? t('auth.twoFactor.useRecoveryCode')
            : codeAvailable ? t('auth.twoFactor.useAuthenticatorApp') : t('auth.twoFactor.usePasskeyInstead')
        }}
      </UButton>
    </div>
  </form>
</template>
