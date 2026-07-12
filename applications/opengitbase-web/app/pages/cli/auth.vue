<script setup lang="ts">
definePageMeta({
  layout: 'auth',
})

const { t } = useI18n()
const route = useRoute()
const runtimeConfig = useRuntimeConfig()

const username = ref('')
const password = ref('')
const loading = ref(false)
const error = ref<string | null>(null)

const queryParams = computed(() =>
  parseCliAuthQueryParams(route.query.port, route.query.state),
)

const hostLabel = computed(() => {
  if (import.meta.client) {
    return window.location.origin
  }

  return runtimeConfig.public.instanceName as string
})

useHead({ title: 'CLI sign in' })

async function onSubmit() {
  if (!queryParams.value) {
    return
  }

  loading.value = true
  error.value = null
  try {
    const api = useApi()
    const result = await api.auth.login({ username: username.value, password: password.value })
    if (result.error || !result.data) {
      error.value = t('auth.signIn.invalidCredentials')
      return
    }

    const callbackUrl = buildCliAuthCallbackUrl(
      queryParams.value.port,
      queryParams.value.state,
      result.data,
    )
    window.location.href = callbackUrl
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
        Sign in for CLI
      </h1>
      <p class="mt-1 text-sm text-[var(--ogb-text-muted)]">
        Authenticating against <span class="font-medium">{{ hostLabel }}</span>
      </p>
      <p class="mt-2 text-sm text-[var(--ogb-text-muted)]">
        Use an existing OpenGitBase account. Registration is available on the web app.
      </p>
    </template>

    <UAlert
      v-if="!queryParams"
      color="error"
      variant="subtle"
      title="Missing CLI auth parameters"
      description="This page requires valid port and state query parameters from the ogb CLI."
      class="mb-4"
    />

    <form
      v-else
      class="space-y-4"
      data-testid="cli-auth-form"
      @submit.prevent="onSubmit"
    >
      <UFormField
        :label="t('auth.fields.username')"
        required
      >
        <UInput
          v-model="username"
          autocomplete="username"
          required
        />
      </UFormField>

      <UFormField
        :label="t('auth.fields.password')"
        required
      >
        <UInput
          v-model="password"
          type="password"
          autocomplete="current-password"
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
        {{ t('auth.signIn.submit') }}
      </UButton>
    </form>
  </UCard>
</template>
