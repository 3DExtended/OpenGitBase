<script setup lang="ts">
import { normalizeAuthenticatorCode } from '~/utils/twoFactor'

const props = withDefaults(defineProps<{
  needsPassword: boolean
  needsCode: boolean
  /** An authenticator app is set up; without one (passkey-only 2FA) a passkey or a recovery code is the second factor. */
  totpConfigured?: boolean
  /** Unused recovery codes exist, so one can stand in for the authenticator code. */
  recoveryAvailable?: boolean
  /**
   * The account has a passkey and the browser can use it. On its own it replaces password and code;
   * with the typed password it replaces the code.
   */
  passkeyAvailable?: boolean
  loading?: boolean
  error?: string | null
}>(), { totpConfigured: true, error: null })

const emit = defineEmits<{
  submit: [credentials: { password: string, code?: string, recoveryCode?: string }]
  /** The password typed so far, empty when none; the page sends it along with the assertion. */
  passkey: [password: string]
  cancel: []
}>()

const { t } = useI18n()
const password = ref('')
const code = ref('')
/** Passkey-only 2FA in a browser without passkeys leaves the recovery code. */
const recoveryOnly = computed(() => props.needsCode && !props.totpConfigured && !props.passkeyAvailable)
const useRecovery = ref(recoveryOnly.value)
/** Accounts without a password and without 2FA can only confirm with a passkey. */
const passkeyOnly = computed(() => !props.needsPassword && !props.needsCode)
/** With passkey-only 2FA there is no code to type: the passkey button, or a recovery code. */
const codeField = computed(() => props.needsCode && (useRecovery.value || props.totpConfigured))
const canSubmit = computed(() => !passkeyOnly.value && (!props.needsCode || codeField.value))
/** Passkey-only 2FA with a password: the passkey is the second step after the password, not an alternative to it. */
const passkeyAfterPassword = computed(() =>
  props.needsPassword && props.needsCode && !props.totpConfigured && props.passkeyAvailable && !useRecovery.value,
)

function toggleMethod() {
  useRecovery.value = !useRecovery.value
  code.value = ''
}

function onSubmit() {
  const secondFactor = !props.needsCode
    ? {}
    : useRecovery.value
      ? { recoveryCode: code.value.trim() }
      : { code: normalizeAuthenticatorCode(code.value) }
  emit('submit', { password: props.needsPassword ? password.value : '', ...secondFactor })
}
</script>

<template>
  <form
    class="space-y-4"
    data-testid="reauthenticate-form"
    @submit.prevent="onSubmit"
  >
    <p class="text-sm text-[var(--ogb-text-muted)]">
      {{ t('settings.security.reauth.description') }}
    </p>

    <template v-if="passkeyAvailable && !passkeyAfterPassword">
      <UButton
        block
        variant="soft"
        icon="i-lucide-fingerprint"
        :loading="loading && passkeyOnly"
        :disabled="loading"
        data-testid="reauth-use-passkey"
        @click="emit('passkey', needsPassword ? password : '')"
      >
        {{ t('settings.security.reauth.usePasskey') }}
      </UButton>
      <USeparator
        v-if="!passkeyOnly"
        :label="t('settings.security.reauth.or')"
      />
    </template>

    <UFormField
      v-if="needsPassword"
      :label="t('auth.fields.password')"
      required
    >
      <UInput
        v-model="password"
        type="password"
        autocomplete="current-password"
        class="w-full"
        required
      />
    </UFormField>

    <template v-if="passkeyAfterPassword">
      <UButton
        block
        icon="i-lucide-fingerprint"
        :loading="loading"
        data-testid="reauth-use-passkey"
        @click="emit('passkey', password)"
      >
        {{ t('settings.security.reauth.usePasskey') }}
      </UButton>
      <p class="text-xs text-[var(--ogb-text-muted)]">
        {{ t('settings.security.reauth.passkeyAfterPasswordHint') }}
      </p>
    </template>

    <template v-if="needsCode">
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
          required
        />
      </UFormField>
      <UFormField
        v-else-if="codeField"
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
      <UButton
        v-if="useRecovery ? !recoveryOnly : recoveryAvailable"
        variant="link"
        size="sm"
        class="px-0"
        :icon="!useRecovery ? 'i-lucide-life-buoy' : totpConfigured ? 'i-lucide-smartphone' : 'i-lucide-fingerprint'"
        data-testid="reauth-toggle-method"
        @click="toggleMethod"
      >
        {{
          !useRecovery
            ? t('auth.twoFactor.useRecoveryCode')
            : totpConfigured ? t('auth.twoFactor.useAuthenticatorApp') : t('auth.twoFactor.usePasskeyInstead')
        }}
      </UButton>
    </template>

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
        v-if="canSubmit"
        type="submit"
        :loading="loading"
      >
        {{ t('settings.security.reauth.submit') }}
      </UButton>
    </div>
  </form>
</template>
