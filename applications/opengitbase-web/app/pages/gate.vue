<script setup lang="ts">
definePageMeta({
  layout: 'auth',
})

const { t } = useI18n()
const route = useRoute()

const password = ref('')
const loading = ref(false)
const error = ref<string | null>(null)

useHead({ title: t('siteGate.title') })

async function onSubmit() {
  loading.value = true
  error.value = null

  if (!unlockSiteGate(password.value)) {
    error.value = t('siteGate.invalidPassword')
    loading.value = false
    return
  }

  const redirect = typeof route.query.redirect === 'string' ? route.query.redirect : '/'
  await navigateTo(redirect)
}
</script>

<template>
  <UCard class="mx-auto w-full max-w-md">
    <template #header>
      <h1 class="text-xl font-semibold">
        {{ t('siteGate.title') }}
      </h1>
      <p class="mt-1 text-sm text-[var(--ogb-text-muted)]">
        {{ t('siteGate.subtitle') }}
      </p>
    </template>

    <form
      class="space-y-4"
      @submit.prevent="onSubmit"
    >
      <UFormField
        :label="t('siteGate.passwordLabel')"
        required
      >
        <UInput
          v-model="password"
          type="password"
          autocomplete="off"
          required
        />
      </UFormField>

      <UAlert
        v-if="error"
        color="error"
        variant="subtle"
        :title="error"
      />

      <UButton
        type="submit"
        block
        :loading="loading"
      >
        {{ t('siteGate.submit') }}
      </UButton>
    </form>
  </UCard>
</template>
