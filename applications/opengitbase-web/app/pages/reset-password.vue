<script setup lang="ts">
definePageMeta({
  middleware: 'guest',
  layout: 'auth',
})

const { t } = useI18n()

const username = ref('')
const email = ref('')
const resetCode = ref('')
const newPassword = ref('')
const loading = ref(false)
const success = ref(false)
const error = ref<string | null>(null)

useHead({ title: t('auth.resetPassword.title') })

async function onSubmit() {
  loading.value = true
  error.value = null
  success.value = false
  try {
    const api = useApi()
    const result = await api.auth.resetPassword({
      username: username.value,
      email: email.value,
      resetCode: resetCode.value,
      newPassword: newPassword.value,
    })
    if (result.error) {
      error.value = t('auth.resetPassword.error')
      return
    }
    success.value = true
  }
  finally {
    loading.value = false
  }
}
</script>

<template>
  <UCard class="mx-auto w-full max-w-md">
    <template #header>
      <h1 class="text-xl font-semibold">
        {{ t('auth.resetPassword.title') }}
      </h1>
      <p class="mt-1 text-sm text-[var(--ogb-text-muted)]">
        {{ t('auth.resetPassword.subtitle') }}
      </p>
    </template>

    <form
      class="space-y-4"
      @submit.prevent="onSubmit"
    >
      <UFormField
        :label="t('auth.fields.username')"
        required
      >
        <UInput
          v-model="username"
          required
        />
      </UFormField>

      <UFormField
        :label="t('auth.fields.email')"
        required
      >
        <UInput
          v-model="email"
          type="email"
          required
        />
      </UFormField>

      <UFormField
        :label="t('auth.fields.resetCode')"
        required
      >
        <UInput
          v-model="resetCode"
          required
        />
      </UFormField>

      <UFormField
        :label="t('auth.fields.newPassword')"
        required
      >
        <UInput
          v-model="newPassword"
          type="password"
          autocomplete="new-password"
          required
        />
      </UFormField>

      <UAlert
        v-if="success"
        color="success"
        variant="subtle"
        :description="t('auth.resetPassword.success')"
      />

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
        {{ t('auth.resetPassword.submit') }}
      </UButton>
    </form>

    <template #footer>
      <NuxtLink
        to="/sign-in"
        class="text-sm text-[var(--ogb-accent)] hover:underline"
      >
        {{ t('auth.resetPassword.backToSignIn') }}
      </NuxtLink>
    </template>
  </UCard>
</template>
