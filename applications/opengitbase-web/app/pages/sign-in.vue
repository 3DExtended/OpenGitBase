<script setup lang="ts">
import type { SecondFactorAnswer, TwoFactorChallenge } from '~/utils/twoFactor'
import { isTwoFactorChallenge, secondFactorErrorKey } from '~/utils/twoFactor'

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
const challenge = ref<TwoFactorChallenge | null>(null)

const {
  available: passkeyAvailable,
  busy: passkeyBusy,
  errorKey: passkeyError,
  signIn: signInWithPasskey,
  pause: pausePasskeyAutofill,
  resume: resumePasskeyAutofill,
} = usePasskeySignIn(async () => {
  await auth.fetchMe()
  await continueToApp()
})

useHead({ title: t('auth.signIn.title') })

async function continueToApp() {
  const redirect = resolveSafeRedirectPath(
    typeof route.query.redirect === 'string' ? route.query.redirect : undefined,
  )
  await navigateTo(redirect)
}

async function onSubmit() {
  loading.value = true
  error.value = null
  pausePasskeyAutofill()
  try {
    const result = await auth.login(username.value, password.value)
    if (result.error) {
      error.value = t('auth.signIn.invalidCredentials')
      void resumePasskeyAutofill()
      return
    }
    if (isTwoFactorChallenge(result.data)) {
      challenge.value = result.data
      return
    }
    await continueToApp()
  }
  finally {
    loading.value = false
  }
}

async function onSubmitCode(answer: SecondFactorAnswer) {
  if (!challenge.value) {
    return
  }
  loading.value = true
  error.value = null
  try {
    const result = await auth.completeTwoFactor(challenge.value.challengeToken, answer)
    if (result.error) {
      if (result.error === 'challenge_expired') {
        backToPassword()
      }
      error.value = t(secondFactorErrorKey(result.error, result.status, 'recoveryCode' in answer ? 'recovery' : 'code'))
      return
    }
    await continueToApp()
  }
  finally {
    loading.value = false
  }
}

function backToPassword() {
  challenge.value = null
  error.value = null
  password.value = ''
  void resumePasskeyAutofill()
}
</script>

<template>
  <UCard class="mx-auto w-full max-w-md">
    <template #header>
      <h1 class="text-xl font-semibold">
        {{ challenge ? t('auth.twoFactor.title') : t('auth.signIn.title') }}
      </h1>
      <p class="mt-1 text-sm text-[var(--ogb-text-muted)]">
        {{ challenge ? t('auth.twoFactor.subtitle') : t('auth.signIn.subtitle') }}
      </p>
    </template>

    <AuthTwoFactorCodeForm
      v-if="challenge"
      :loading="loading"
      :error="error"
      :recovery-available="challenge.methods.includes('recovery')"
      @submit="onSubmitCode"
      @switch-method="error = null"
      @back="backToPassword"
    />

    <form
      v-else
      class="space-y-4"
      @submit.prevent="onSubmit"
    >
      <UFormField
        :label="t('auth.fields.username')"
        required
      >
        <UInput
          v-model="username"
          autocomplete="username webauthn"
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

      <AuthPasskeySignInButton
        v-if="passkeyAvailable"
        :loading="passkeyBusy"
        :error="passkeyError"
        @click="signInWithPasskey"
      />
    </form>

    <template
      v-if="!challenge"
      #footer
    >
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
