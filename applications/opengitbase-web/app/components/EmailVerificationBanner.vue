<script setup lang="ts">
const auth = useAuth()
const { t } = useI18n()

const resending = ref(false)
const resent = ref(false)
const error = ref<string | null>(null)

async function resend() {
  resending.value = true
  error.value = null
  resent.value = false
  try {
    const api = useApi()
    const result = await api.account.resendVerification()
    if (result.error) {
      error.value = result.error
      return
    }
    resent.value = true
  }
  finally {
    resending.value = false
  }
}
</script>

<template>
  <UAlert
    v-if="auth.isAuthenticated && !auth.isEmailVerified"
    color="warning"
    variant="subtle"
    icon="i-lucide-mail-warning"
    class="mb-6"
    :title="t('verification.bannerTitle')"
    :description="t('verification.bannerDescription')"
  >
    <template #actions>
      <div class="flex flex-wrap items-center justify-end gap-2">
        <UButton
          size="xs"
          color="warning"
          variant="soft"
          :loading="resending"
          @click="resend"
        >
          {{ t('verification.resend') }}
        </UButton>
        <EmailVerificationDebugActions />
      </div>
    </template>
    <p
      v-if="resent"
      class="mt-2 text-xs"
    >
      {{ t('verification.resent') }}
    </p>
    <p
      v-if="error"
      class="mt-2 text-xs text-error"
    >
      {{ error }}
    </p>
  </UAlert>
</template>
