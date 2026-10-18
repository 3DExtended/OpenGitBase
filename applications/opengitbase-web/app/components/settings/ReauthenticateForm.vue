<script setup lang="ts">
import { normalizeAuthenticatorCode } from '~/utils/twoFactor'

const props = defineProps<{
  needsPassword: boolean
  needsCode: boolean
  /** Unused recovery codes exist, so one can stand in for the authenticator code. */
  recoveryAvailable?: boolean
  /** The account has a passkey and the browser can use it; it replaces password and code. */
  passkeyAvailable?: boolean
  loading?: boolean
  error?: string | null
}>()

const emit = defineEmits<{
  submit: [credentials: { password: string, code?: string, recoveryCode?: string }]
  passkey: []
  cancel: []
}>()

const { t } = useI18n()
const password = ref('')
const code = ref('')
const useRecovery = ref(false)
/** Accounts without a password and without 2FA can only confirm with a passkey. */
const passkeyOnly = computed(() => !props.needsPassword && !props.needsCode)

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

    <template v-if="passkeyAvailable">
      <UButton
        block
        variant="soft"
        icon="i-lucide-fingerprint"
        :loading="loading && passkeyOnly"
        :disabled="loading"
        data-testid="reauth-use-passkey"
        @click="emit('passkey')"
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
          required
        />
      </UFormField>
      <UButton
        v-if="recoveryAvailable || useRecovery"
        variant="link"
        size="sm"
        class="px-0"
        :icon="useRecovery ? 'i-lucide-smartphone' : 'i-lucide-life-buoy'"
        @click="toggleMethod"
      >
        {{ useRecovery ? t('auth.twoFactor.useAuthenticatorApp') : t('auth.twoFactor.useRecoveryCode') }}
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
        v-if="!passkeyOnly"
        type="submit"
        :loading="loading"
      >
        {{ t('settings.security.reauth.submit') }}
      </UButton>
    </div>
  </form>
</template>
