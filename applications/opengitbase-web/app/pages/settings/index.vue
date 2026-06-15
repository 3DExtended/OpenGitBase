<script setup lang="ts">
definePageMeta({ middleware: 'auth' })

const { t } = useI18n()
const auth = useAuth()
const api = useApi()

const currentPassword = ref('')
const newPassword = ref('')
const changeLoading = ref(false)
const changeError = ref<string | null>(null)
const changeSuccess = ref(false)

const deletePassword = ref('')
const deleteLoading = ref(false)
const deleteError = ref<string | null>(null)
const deleteBlockers = ref<Array<{ type: string, name: string, slug: string }>>([])
const showDeleteConfirm = ref(false)

useHead({ title: t('settings.title') })

async function changePassword() {
  changeLoading.value = true
  changeError.value = null
  changeSuccess.value = false
  try {
    const result = await api.account.changePassword({
      currentPassword: currentPassword.value,
      newPassword: newPassword.value,
    })
    if (result.error) {
      changeError.value = t('settings.changePassword.error')
      return
    }
    changeSuccess.value = true
    currentPassword.value = ''
    newPassword.value = ''
  }
  finally {
    changeLoading.value = false
  }
}

async function deleteAccount() {
  deleteLoading.value = true
  deleteError.value = null
  deleteBlockers.value = []
  try {
    const result = await api.account.deleteAccount({ password: deletePassword.value })
    if (result.data?.blockers?.length) {
      deleteBlockers.value = result.data.blockers
      return
    }
    if (result.error || !result.data?.success) {
      deleteError.value = result.error ?? t('settings.deleteAccount.error')
      return
    }
    await auth.signOut()
    await navigateTo('/')
  }
  finally {
    deleteLoading.value = false
    showDeleteConfirm.value = false
  }
}
</script>

<template>
  <div class="mx-auto max-w-2xl space-y-6">
    <EmailVerificationBanner />

    <div>
      <h1 class="text-2xl font-semibold">
        {{ t('settings.title') }}
      </h1>
      <p class="mt-1 text-sm text-[var(--ogb-text-muted)]">
        {{ t('settings.subtitle') }}
      </p>
    </div>

    <UCard>
      <template #header>
        <h2 class="font-semibold">
          {{ t('settings.profile.title') }}
        </h2>
      </template>
      <dl class="space-y-3 text-sm">
        <div class="flex justify-between gap-4">
          <dt class="text-[var(--ogb-text-muted)]">
            {{ t('auth.fields.username') }}
          </dt>
          <dd class="font-medium">
            {{ auth.user?.username }}
          </dd>
        </div>
        <div class="flex justify-between gap-4">
          <dt class="text-[var(--ogb-text-muted)]">
            {{ t('settings.profile.emailVerified') }}
          </dt>
          <dd>
            <UBadge
              :color="auth.isEmailVerified ? 'success' : 'warning'"
              variant="subtle"
            >
              {{ auth.isEmailVerified ? t('settings.profile.verified') : t('settings.profile.unverified') }}
            </UBadge>
          </dd>
        </div>
      </dl>
      <EmailVerificationDebugPanel />
      <template #footer>
        <UButton
          to="/settings/ssh-keys"
          variant="soft"
        >
          {{ t('settings.sshKeys.link') }}
        </UButton>
      </template>
    </UCard>

    <UCard>
      <template #header>
        <h2 class="font-semibold">
          {{ t('settings.changePassword.title') }}
        </h2>
      </template>
      <form
        class="space-y-4"
        @submit.prevent="changePassword"
      >
        <UFormField
          :label="t('settings.changePassword.current')"
          required
        >
          <UInput
            v-model="currentPassword"
            type="password"
            autocomplete="current-password"
          />
        </UFormField>
        <UFormField
          :label="t('settings.changePassword.new')"
          required
        >
          <UInput
            v-model="newPassword"
            type="password"
            autocomplete="new-password"
          />
        </UFormField>
        <UAlert
          v-if="changeSuccess"
          color="success"
          variant="subtle"
          :description="t('settings.changePassword.success')"
        />
        <UAlert
          v-if="changeError"
          color="error"
          variant="subtle"
          :description="changeError"
        />
        <UButton
          type="submit"
          :loading="changeLoading"
        >
          {{ t('settings.changePassword.submit') }}
        </UButton>
      </form>
    </UCard>

    <UCard>
      <template #header>
        <h2 class="font-semibold text-error">
          {{ t('settings.deleteAccount.title') }}
        </h2>
      </template>
      <p class="text-sm text-[var(--ogb-text-muted)]">
        {{ t('settings.deleteAccount.warning') }}
      </p>

      <UAlert
        v-if="deleteBlockers.length"
        color="warning"
        variant="subtle"
        class="mt-4"
        :title="t('settings.deleteAccount.blockedTitle')"
      >
        <ul class="mt-2 list-inside list-disc text-sm">
          <li
            v-for="blocker in deleteBlockers"
            :key="`${blocker.type}-${blocker.slug}`"
          >
            {{ blocker.type }}: {{ blocker.name }} ({{ blocker.slug }})
          </li>
        </ul>
      </UAlert>

      <UAlert
        v-if="deleteError"
        color="error"
        variant="subtle"
        class="mt-4"
        :description="deleteError"
      />

      <UButton
        color="error"
        variant="soft"
        class="mt-4"
        @click="showDeleteConfirm = true"
      >
        {{ t('settings.deleteAccount.button') }}
      </UButton>
    </UCard>

    <UModal v-model:open="showDeleteConfirm">
      <template #content>
        <UCard>
          <template #header>
            <h3 class="font-semibold">
              {{ t('settings.deleteAccount.confirmTitle') }}
            </h3>
          </template>
          <p class="text-sm text-[var(--ogb-text-muted)]">
            {{ t('settings.deleteAccount.confirmDescription') }}
          </p>
          <UFormField
            :label="t('auth.fields.password')"
            class="mt-4"
            required
          >
            <UInput
              v-model="deletePassword"
              type="password"
            />
          </UFormField>
          <div class="mt-4 flex justify-end gap-2">
            <UButton
              variant="ghost"
              @click="showDeleteConfirm = false"
            >
              {{ t('common.cancel') }}
            </UButton>
            <UButton
              color="error"
              :loading="deleteLoading"
              @click="deleteAccount"
            >
              {{ t('settings.deleteAccount.confirmButton') }}
            </UButton>
          </div>
        </UCard>
      </template>
    </UModal>
  </div>
</template>
