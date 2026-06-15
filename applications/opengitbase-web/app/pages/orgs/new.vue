<script setup lang="ts">
import { validateSlug } from '~/utils/slug-validation'

definePageMeta({ middleware: 'auth' })

const { t } = useI18n()
const api = useApi()

const name = ref('')
const slugError = ref<string | null>(null)
const loading = ref(false)
const error = ref<string | null>(null)

useHead({ title: t('org.create.title') })

watch(name, (value) => {
  const key = validateSlug(value)
  slugError.value = key ? t(`auth.signUp.errors.${key}`) : null
})

async function onSubmit() {
  const key = validateSlug(name.value)
  if (key) {
    slugError.value = t(`auth.signUp.errors.${key}`)
    return
  }

  loading.value = true
  error.value = null
  try {
    const result = await api.organizations.create({
      modelToCreate: { name: name.value },
    })
    if (result.error) {
      error.value = result.error
      return
    }
    await navigateTo(`/${name.value}`)
  }
  finally {
    loading.value = false
  }
}
</script>

<template>
  <div class="mx-auto max-w-lg space-y-6">
    <EmailVerificationBanner />

    <div>
      <h1 class="text-2xl font-semibold">
        {{ t('org.create.title') }}
      </h1>
      <p class="mt-1 text-sm text-[var(--ogb-text-muted)]">
        {{ t('org.create.subtitle') }}
      </p>
    </div>

    <UCard>
      <form
        class="space-y-4"
        @submit.prevent="onSubmit"
      >
        <UFormField
          :label="t('org.create.nameLabel')"
          :error="slugError ?? undefined"
          required
        >
          <UInput v-model="name" />
          <template #hint>
            <span class="text-xs text-[var(--ogb-text-muted)]">{{ t('org.create.nameHint') }}</span>
          </template>
        </UFormField>

        <UAlert
          v-if="error"
          color="error"
          variant="subtle"
          :description="error"
        />

        <UButton
          type="submit"
          :loading="loading"
          :disabled="!!slugError"
        >
          {{ t('org.create.submit') }}
        </UButton>
      </form>
    </UCard>
  </div>
</template>
