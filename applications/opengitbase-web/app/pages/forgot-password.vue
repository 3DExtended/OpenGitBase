<script setup lang="ts">
definePageMeta({
  middleware: 'guest',
  layout: 'auth',
})

const { t } = useI18n()

const username = ref('')
const email = ref('')
const loading = ref(false)
const success = ref(false)
const error = ref<string | null>(null)

useHead({ title: t('auth.forgotPassword.title') })

async function onSubmit() {
  loading.value = true
  error.value = null
  success.value = false
  try {
    const api = useApi()
    const result = await api.auth.requestPasswordReset({
      username: username.value,
      email: email.value,
    })
    if (result.error) {
      error.value = t('auth.forgotPassword.error')
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
        {{ t('auth.forgotPassword.title') }}
      </h1>
      <p class="mt-1 text-sm text-[var(--ogb-text-muted)]">
        {{ t('auth.forgotPassword.subtitle') }}
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

      <UAlert
        v-if="success"
        color="success"
        variant="subtle"
        :description="t('auth.forgotPassword.success')"
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
        {{ t('auth.forgotPassword.submit') }}
      </UButton>
    </form>

    <template #footer>
      <NuxtLink
        to="/sign-in"
        class="text-sm text-[var(--ogb-accent)] hover:underline"
      >
        {{ t('auth.forgotPassword.backToSignIn') }}
      </NuxtLink>
    </template>
  </UCard>
</template>
