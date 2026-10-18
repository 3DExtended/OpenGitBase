<script setup lang="ts">
import type { ApiResult } from '~/utils/api'
import type { AccountSecurity, TotpSetup } from '~/utils/twoFactor'
import type { Passkey } from '~/utils/webauthn'
import { secondFactorErrorKey } from '~/utils/twoFactor'
import {
  createPasskey,
  getPasskeyAssertion,
  passkeyApiErrorKey,
  passkeyErrorKey,
  webAuthnSupported,
} from '~/utils/webauthn'

definePageMeta({ middleware: 'auth' })

const { t } = useI18n()
const api = useApi()
const auth = useAuth()

const status = ref<AccountSecurity | null>(null)
const loading = ref(true)
const loadError = ref<string | null>(null)
const busy = ref(false)
const actionError = ref<string | null>(null)
const notice = ref<string | null>(null)

const setup = ref<TotpSetup | null>(null)
const confirmLoading = ref(false)
const confirmError = ref<string | null>(null)

const recoveryCodes = ref<string[] | null>(null)
const showRegenerateConfirm = ref(false)

const passkeys = ref<Passkey[]>([])
const webAuthnReady = ref(false)
const passkeyBusy = ref(false)
const passkeyError = ref<string | null>(null)
const addingPasskey = ref(false)
const passkeyToRemove = ref<Passkey | null>(null)

const showDisableConfirm = ref(false)
const showReauth = ref(false)
const reauthLoading = ref(false)
const reauthError = ref<string | null>(null)
let retryAfterReauth: (() => Promise<void>) | null = null

useHead({ title: t('settings.security.title') })

const showTwoFactor = computed(() =>
  status.value !== null && (status.value.twoFactorAvailable || status.value.twoFactorEnabled),
)

/** Hidden while `WebAuthn:RelyingPartyId` is empty and in browsers without WebAuthn. */
const showPasskeys = computed(() => webAuthnReady.value && (status.value?.passkeysAvailable ?? false))
const canReauthenticateWithPasskey = computed(() => showPasskeys.value && passkeys.value.length > 0)

async function load() {
  loading.value = true
  loadError.value = null
  try {
    const result = await api.account.security()
    if (result.error || !result.data) {
      loadError.value = result.error ?? t('auth.twoFactor.errors.generic')
      return
    }
    status.value = result.data
    if (showPasskeys.value) {
      await loadPasskeys()
    }
  }
  finally {
    loading.value = false
  }
}

async function loadPasskeys() {
  const result = await api.account.passkeys()
  if (result.error || !result.data) {
    passkeyError.value = t(passkeyApiErrorKey(result.error, result.status))
    return
  }
  passkeys.value = result.data
}

/**
 * Sensitive changes need a recent re-authentication of this session. When the API asks for one,
 * collect it and run the action again.
 */
function needsReauthentication(result: ApiResult<unknown>, retry: () => Promise<void>): boolean {
  if (result.status !== 403 || result.error !== 'reauthentication_required') {
    return false
  }
  if (status.value && !status.value.hasPassword && !status.value.twoFactorEnabled && !canReauthenticateWithPasskey.value) {
    actionError.value = t('auth.twoFactor.errors.reauthenticationUnavailable')
    return true
  }
  retryAfterReauth = retry
  reauthError.value = null
  showReauth.value = true
  return true
}

async function startSetup() {
  busy.value = true
  actionError.value = null
  notice.value = null
  try {
    const result = await api.account.startTotpSetup()
    if (needsReauthentication(result, startSetup)) {
      return
    }
    if (result.error || !result.data) {
      actionError.value = t(secondFactorErrorKey(result.error, result.status))
      return
    }
    confirmError.value = null
    setup.value = result.data
  }
  finally {
    busy.value = false
  }
}

async function confirmSetup(code: string) {
  confirmLoading.value = true
  confirmError.value = null
  try {
    const result = await api.account.confirmTotpSetup({ code })
    if (needsReauthentication(result, () => confirmSetup(code))) {
      return
    }
    if (result.error) {
      confirmError.value = t(secondFactorErrorKey(result.error, result.status))
      return
    }
    setup.value = null
    recoveryCodes.value = result.data?.recoveryCodes?.length ? result.data.recoveryCodes : null
    notice.value = t('settings.security.enabledNotice')
    await Promise.all([load(), auth.fetchMe()])
  }
  finally {
    confirmLoading.value = false
  }
}

async function disable() {
  showDisableConfirm.value = false
  busy.value = true
  actionError.value = null
  notice.value = null
  try {
    const result = await api.account.disableTwoFactor()
    if (needsReauthentication(result, disable)) {
      return
    }
    if (result.error) {
      actionError.value = t(secondFactorErrorKey(result.error, result.status))
      return
    }
    notice.value = t('settings.security.disabledNotice')
    await Promise.all([load(), auth.fetchMe()])
  }
  finally {
    busy.value = false
  }
}

async function regenerate() {
  showRegenerateConfirm.value = false
  busy.value = true
  actionError.value = null
  notice.value = null
  try {
    const result = await api.account.regenerateRecoveryCodes()
    if (needsReauthentication(result, regenerate)) {
      return
    }
    if (result.error || !result.data) {
      actionError.value = t(secondFactorErrorKey(result.error, result.status))
      return
    }
    recoveryCodes.value = result.data.recoveryCodes
    await load()
  }
  finally {
    busy.value = false
  }
}

async function addPasskey(name: string) {
  passkeyBusy.value = true
  passkeyError.value = null
  notice.value = null
  try {
    const options = await api.account.passkeyRegistrationOptions()
    if (needsReauthentication(options, () => addPasskey(name))) {
      return
    }
    if (options.error || !options.data) {
      passkeyError.value = t(passkeyApiErrorKey(options.error, options.status))
      return
    }
    let attestationResponse
    try {
      attestationResponse = await createPasskey(options.data)
    }
    catch (error) {
      passkeyError.value = t(passkeyErrorKey(error))
      return
    }
    const result = await api.account.registerPasskey({ name, attestationResponse })
    if (needsReauthentication(result, () => addPasskey(name))) {
      return
    }
    if (result.error) {
      passkeyError.value = t(passkeyApiErrorKey(result.error, result.status))
      return
    }
    addingPasskey.value = false
    notice.value = t('settings.security.passkeys.addedNotice')
    await loadPasskeys()
  }
  finally {
    passkeyBusy.value = false
  }
}

async function renamePasskey(id: string, name: string) {
  passkeyError.value = null
  const result = await api.account.renamePasskey(id, { name })
  if (result.error || !result.data) {
    passkeyError.value = t(passkeyApiErrorKey(result.error, result.status))
    return
  }
  const renamed = result.data
  passkeys.value = passkeys.value.map(passkey => (passkey.id === renamed.id ? renamed : passkey))
}

async function removePasskey() {
  const passkey = passkeyToRemove.value
  passkeyToRemove.value = null
  if (passkey) {
    await deletePasskey(passkey)
  }
}

async function deletePasskey(passkey: Passkey) {
  passkeyBusy.value = true
  passkeyError.value = null
  notice.value = null
  try {
    const result = await api.account.deletePasskey(passkey.id)
    if (needsReauthentication(result, () => deletePasskey(passkey))) {
      return
    }
    if (result.error) {
      passkeyError.value = t(passkeyApiErrorKey(result.error, result.status))
      return
    }
    notice.value = t('settings.security.passkeys.removedNotice')
    await loadPasskeys()
  }
  finally {
    passkeyBusy.value = false
  }
}

async function finishRecoveryCodes() {
  recoveryCodes.value = null
  notice.value = null
  await load()
}

async function reauthenticate(credentials: { password: string, code?: string, recoveryCode?: string }) {
  reauthLoading.value = true
  reauthError.value = null
  try {
    const result = await api.account.reauthenticate(credentials)
    if (result.error) {
      reauthError.value = t(secondFactorErrorKey(
        result.error,
        result.status,
        credentials.recoveryCode ? 'recovery' : 'code',
      ))
      return
    }
    // A recovery code was just spent; keep the count shown behind the dialog honest.
    if (credentials.recoveryCode) {
      await load()
    }
    await finishReauthentication()
  }
  finally {
    reauthLoading.value = false
  }
}

async function reauthenticateWithPasskey() {
  reauthLoading.value = true
  reauthError.value = null
  try {
    const options = await api.account.passkeyReauthenticationOptions()
    if (options.error || !options.data) {
      reauthError.value = t(passkeyApiErrorKey(options.error, options.status))
      return
    }
    let passkeyAssertion
    try {
      passkeyAssertion = await getPasskeyAssertion(options.data)
    }
    catch (error) {
      reauthError.value = t(passkeyErrorKey(error))
      return
    }
    const result = await api.account.reauthenticate({ passkeyAssertion })
    if (result.error) {
      reauthError.value = t(passkeyApiErrorKey(result.error, result.status))
      return
    }
    await finishReauthentication()
  }
  finally {
    reauthLoading.value = false
  }
}

async function finishReauthentication() {
  showReauth.value = false
  const retry = retryAfterReauth
  retryAfterReauth = null
  await retry?.()
}

function cancelReauth() {
  showReauth.value = false
  retryAfterReauth = null
}

onMounted(() => {
  webAuthnReady.value = webAuthnSupported()
  return load()
})
</script>

<template>
  <div class="mx-auto max-w-2xl space-y-6">
    <div>
      <UButton
        to="/settings"
        variant="ghost"
        icon="i-lucide-arrow-left"
        size="sm"
        class="mb-4"
      >
        {{ t('settings.title') }}
      </UButton>
      <h1 class="text-2xl font-semibold">
        {{ t('settings.security.title') }}
      </h1>
      <p class="mt-1 text-sm text-[var(--ogb-text-muted)]">
        {{ t('settings.security.subtitle') }}
      </p>
    </div>

    <div
      v-if="loading && !status"
      class="text-sm text-[var(--ogb-text-muted)]"
    >
      {{ t('common.loading') }}
    </div>

    <UAlert
      v-else-if="loadError"
      color="error"
      variant="subtle"
      :description="loadError"
    />

    <template v-else-if="status">
      <UAlert
        v-if="notice"
        color="success"
        variant="subtle"
        :description="notice"
      />
      <UAlert
        v-if="actionError"
        color="error"
        variant="subtle"
        :description="actionError"
      />

      <template v-if="showTwoFactor">
        <UCard v-if="setup">
          <template #header>
            <h2 class="font-semibold">
              {{ t('settings.security.setup.title') }}
            </h2>
          </template>
          <SettingsTotpSetupPanel
            :setup="setup"
            :loading="confirmLoading"
            :error="confirmError"
            @confirm="confirmSetup"
            @cancel="setup = null"
          />
        </UCard>

        <UCard v-else-if="recoveryCodes">
          <template #header>
            <h2 class="font-semibold">
              {{ t('settings.security.recoveryCodes.title') }}
            </h2>
          </template>
          <SettingsRecoveryCodesPanel
            :codes="recoveryCodes"
            :account="auth.user?.username ?? ''"
            @done="finishRecoveryCodes"
          />
        </UCard>

        <SettingsTwoFactorStatusCard
          v-else
          :status="status"
          :busy="busy"
          @setup="startSetup"
          @disable="showDisableConfirm = true"
          @regenerate="showRegenerateConfirm = true"
        />
      </template>

      <UCard v-else>
        <p class="text-sm text-[var(--ogb-text-muted)]">
          {{ t('settings.security.unavailable') }}
        </p>
      </UCard>

      <SettingsPasskeysCard
        v-if="showPasskeys"
        v-model:adding="addingPasskey"
        :passkeys="passkeys"
        :busy="passkeyBusy"
        :error="passkeyError"
        @add="addPasskey"
        @rename="renamePasskey"
        @remove="passkeyToRemove = $event"
      />
    </template>

    <UModal v-model:open="showDisableConfirm">
      <template #content>
        <UCard>
          <template #header>
            <h3 class="font-semibold">
              {{ t('settings.security.twoFactor.disableConfirmTitle') }}
            </h3>
          </template>
          <p class="text-sm text-[var(--ogb-text-muted)]">
            {{ t('settings.security.twoFactor.disableConfirmDescription') }}
          </p>
          <div class="mt-4 flex justify-end gap-2">
            <UButton
              variant="ghost"
              @click="showDisableConfirm = false"
            >
              {{ t('common.cancel') }}
            </UButton>
            <UButton
              color="error"
              :loading="busy"
              @click="disable"
            >
              {{ t('settings.security.twoFactor.disableConfirmButton') }}
            </UButton>
          </div>
        </UCard>
      </template>
    </UModal>

    <UModal v-model:open="showRegenerateConfirm">
      <template #content>
        <UCard>
          <template #header>
            <h3 class="font-semibold">
              {{ t('settings.security.twoFactor.regenerateConfirmTitle') }}
            </h3>
          </template>
          <p class="text-sm text-[var(--ogb-text-muted)]">
            {{ t('settings.security.twoFactor.regenerateConfirmDescription') }}
          </p>
          <div class="mt-4 flex justify-end gap-2">
            <UButton
              variant="ghost"
              @click="showRegenerateConfirm = false"
            >
              {{ t('common.cancel') }}
            </UButton>
            <UButton
              color="warning"
              :loading="busy"
              data-testid="recovery-codes-regenerate-confirm"
              @click="regenerate"
            >
              {{ t('settings.security.twoFactor.regenerateConfirmButton') }}
            </UButton>
          </div>
        </UCard>
      </template>
    </UModal>

    <UModal
      :open="passkeyToRemove !== null"
      @update:open="open => { if (!open) passkeyToRemove = null }"
    >
      <template #content>
        <UCard>
          <template #header>
            <h3 class="font-semibold">
              {{ t('settings.security.passkeys.removeConfirmTitle') }}
            </h3>
          </template>
          <p class="text-sm text-[var(--ogb-text-muted)]">
            {{ t('settings.security.passkeys.removeConfirmDescription', { name: passkeyToRemove?.name ?? '' }) }}
          </p>
          <div class="mt-4 flex justify-end gap-2">
            <UButton
              variant="ghost"
              @click="passkeyToRemove = null"
            >
              {{ t('common.cancel') }}
            </UButton>
            <UButton
              color="error"
              :loading="passkeyBusy"
              data-testid="passkey-remove-confirm"
              @click="removePasskey"
            >
              {{ t('settings.security.passkeys.removeConfirmButton') }}
            </UButton>
          </div>
        </UCard>
      </template>
    </UModal>

    <UModal v-model:open="showReauth">
      <template #content>
        <UCard>
          <template #header>
            <h3 class="font-semibold">
              {{ t('settings.security.reauth.title') }}
            </h3>
          </template>
          <SettingsReauthenticateForm
            :needs-password="status?.hasPassword ?? true"
            :needs-code="status?.twoFactorEnabled ?? false"
            :recovery-available="(status?.recoveryCodesRemaining ?? 0) > 0"
            :passkey-available="canReauthenticateWithPasskey"
            :loading="reauthLoading"
            :error="reauthError"
            @submit="reauthenticate"
            @passkey="reauthenticateWithPasskey"
            @cancel="cancelReauth"
          />
        </UCard>
      </template>
    </UModal>
  </div>
</template>
