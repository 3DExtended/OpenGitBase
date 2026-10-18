<script setup lang="ts">
import type { ApiResult } from '~/utils/api'
import type { AccountSecurity, TotpSetup } from '~/utils/twoFactor'
import { secondFactorErrorKey } from '~/utils/twoFactor'

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

const showDisableConfirm = ref(false)
const showReauth = ref(false)
const reauthLoading = ref(false)
const reauthError = ref<string | null>(null)
let retryAfterReauth: (() => Promise<void>) | null = null

useHead({ title: t('settings.security.title') })

const showTwoFactor = computed(() =>
  status.value !== null && (status.value.twoFactorAvailable || status.value.twoFactorEnabled),
)

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
  }
  finally {
    loading.value = false
  }
}

/**
 * Sensitive changes need a recent re-authentication of this session. When the API asks for one,
 * collect it and run the action again.
 */
function needsReauthentication(result: ApiResult<unknown>, retry: () => Promise<void>): boolean {
  if (result.status !== 403 || result.error !== 'reauthentication_required') {
    return false
  }
  if (status.value && !status.value.hasPassword && !status.value.twoFactorEnabled) {
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

async function reauthenticate(credentials: { password: string, code?: string }) {
  reauthLoading.value = true
  reauthError.value = null
  try {
    const result = await api.account.reauthenticate(credentials)
    if (result.error) {
      reauthError.value = t(secondFactorErrorKey(result.error, result.status))
      return
    }
    showReauth.value = false
    const retry = retryAfterReauth
    retryAfterReauth = null
    await retry?.()
  }
  finally {
    reauthLoading.value = false
  }
}

function cancelReauth() {
  showReauth.value = false
  retryAfterReauth = null
}

onMounted(load)
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

        <SettingsTwoFactorStatusCard
          v-else
          :status="status"
          :busy="busy"
          @setup="startSetup"
          @disable="showDisableConfirm = true"
        />
      </template>

      <UCard v-else>
        <p class="text-sm text-[var(--ogb-text-muted)]">
          {{ t('settings.security.unavailable') }}
        </p>
      </UCard>
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
            :loading="reauthLoading"
            :error="reauthError"
            @submit="reauthenticate"
            @cancel="cancelReauth"
          />
        </UCard>
      </template>
    </UModal>
  </div>
</template>
