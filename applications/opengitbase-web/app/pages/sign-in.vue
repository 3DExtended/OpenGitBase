<script setup lang="ts">
definePageMeta({
  middleware: 'guest',
  layout: 'auth',
})

const { t } = useI18n()
const route = useRoute()
const auth = useAuth()

const username = ref('')
const password = ref('')
const loading = ref(false)
const error = ref<string | null>(null)

useHead({ title: t('auth.signIn.title') })

async function onSubmit() {
  loading.value = true
  error.value = null
  try {
    const result = await auth.login(username.value, password.value)
    if (result.error) {
      error.value = t('auth.signIn.invalidCredentials')
      return
    }
    const redirect = typeof route.query.redirect === 'string' ? route.query.redirect : '/'
    await navigateTo(redirect)
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
        {{ t('auth.signIn.title') }}
      </h1>
      <p class="mt-1 text-sm text-[var(--ogb-text-muted)]">
        {{ t('auth.signIn.subtitle') }}
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

    <template #footer>
      <div class="flex flex-col gap-2 text-sm">
        <NuxtLink
          to="/forgot-password"
          class="text-[var(--ogb-accent)] hover:underline"
        >
          {{ t('auth.signIn.forgotPassword') }}
        </NuxtLink>
        <p>
          {{ t('auth.signIn.noAccount') }}
          <NuxtLink
            to="/sign-up"
            class="text-[var(--ogb-accent)] hover:underline"
          >
            {{ t('nav.signUp') }}
          </NuxtLink>
        </p>
      </div>
    </template>
  </UCard>
</template>
