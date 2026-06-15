<script setup lang="ts">
import { validateSlug } from '~/utils/slug-validation'

definePageMeta({
  middleware: 'guest',
  layout: 'auth',
})

const { t } = useI18n()
const auth = useAuth()

const username = ref('')
const email = ref('')
const password = ref('')
const loading = ref(false)
const error = ref<string | null>(null)
const slugError = ref<string | null>(null)

useHead({ title: t('auth.signUp.title') })

watch(username, (value) => {
  const key = validateSlug(value)
  slugError.value = key ? t(`auth.signUp.errors.${key}`) : null
})

async function onSubmit() {
  const key = validateSlug(username.value)
  if (key) {
    slugError.value = t(`auth.signUp.errors.${key}`)
    return
  }

  loading.value = true
  error.value = null
  try {
    const result = await auth.register(username.value, email.value, password.value)
    if (result.error) {
      if (result.error.toLowerCase().includes('username')) {
        error.value = t('auth.signUp.errors.usernameTaken')
      }
      else if (result.error.toLowerCase().includes('reserved')) {
        error.value = t('auth.signUp.errors.slug.reserved')
      }
      else if (result.error.toLowerCase().includes('email')) {
        error.value = t('auth.signUp.errors.emailTaken')
      }
      else {
        error.value = result.error
      }
      return
    }
    await navigateTo('/')
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
        {{ t('auth.signUp.title') }}
      </h1>
      <p class="mt-1 text-sm text-[var(--ogb-text-muted)]">
        {{ t('auth.signUp.subtitle') }}
      </p>
    </template>

    <form
      class="space-y-4"
      @submit.prevent="onSubmit"
    >
      <UFormField
        :label="t('auth.fields.username')"
        :error="slugError ?? undefined"
        required
      >
        <UInput
          v-model="username"
          autocomplete="username"
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
          autocomplete="email"
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
          autocomplete="new-password"
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
        :disabled="!!slugError"
      >
        {{ t('auth.signUp.submit') }}
      </UButton>
    </form>

    <template #footer>
      <p class="text-sm">
        {{ t('auth.signUp.hasAccount') }}
        <NuxtLink
          to="/sign-in"
          class="text-[var(--ogb-accent)] hover:underline"
        >
          {{ t('nav.signIn') }}
        </NuxtLink>
      </p>
    </template>
  </UCard>
</template>
