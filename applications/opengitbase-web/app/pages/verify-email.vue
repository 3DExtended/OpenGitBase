<script setup lang="ts">
definePageMeta({
  layout: 'auth',
})

const { t } = useI18n()
const route = useRoute()
const auth = useAuth()

const status = ref<'pending' | 'success' | 'error'>('pending')
const message = ref('')

useHead({ title: t('verification.pageTitle') })

onMounted(async () => {
  const username = typeof route.query.username === 'string' ? route.query.username : ''
  const token = typeof route.query.token === 'string' ? route.query.token : ''

  if (!username || !token) {
    status.value = 'error'
    message.value = t('verification.invalidLink')
    return
  }

  const api = useApi()
  const result = await api.account.verifyEmail({
    username,
    verificationToken: token,
  })

  if (result.error) {
    status.value = 'error'
    message.value = t('verification.failed')
    return
  }

  status.value = 'success'
  message.value = t('verification.success')
  auth.setEmailVerified(true)
})
</script>

<template>
  <UCard class="mx-auto w-full max-w-md text-center">
    <template #header>
      <h1 class="text-xl font-semibold">
        {{ t('verification.pageTitle') }}
      </h1>
    </template>

    <div class="space-y-4">
      <UIcon
        v-if="status === 'pending'"
        name="i-lucide-loader-circle"
        class="mx-auto size-10 animate-spin text-[var(--ogb-accent)]"
      />
      <UIcon
        v-else-if="status === 'success'"
        name="i-lucide-circle-check"
        class="mx-auto size-10 text-success"
      />
      <UIcon
        v-else
        name="i-lucide-circle-x"
        class="mx-auto size-10 text-error"
      />

      <p class="text-sm text-[var(--ogb-text-muted)]">
        {{ message || t('verification.pending') }}
      </p>

      <UButton
        v-if="status !== 'pending'"
        to="/"
        block
      >
        {{ t('verification.continue') }}
      </UButton>
    </div>
  </UCard>
</template>
