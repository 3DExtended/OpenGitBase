<script setup lang="ts">
import { slugify, validateSlug } from '~/utils/slug-validation'

definePageMeta({ middleware: 'auth' })

const { t } = useI18n()
const auth = useAuth()
const api = useApi()

const name = ref('')
const slug = ref('')
const isPrivate = ref(false)
const ownerType = ref<'user' | 'organization'>('user')
const ownerOrg = ref('')
const slugTouched = ref(false)
const slugError = ref<string | null>(null)
const loading = ref(false)
const error = ref<string | null>(null)

const organizations = ref<Array<{ id: string, name: string }>>([])

useHead({ title: t('repo.create.title') })

watch(name, (value) => {
  if (!slugTouched.value) {
    slug.value = slugify(value)
  }
})

watch(slug, (value) => {
  const key = validateSlug(value)
  slugError.value = key ? t(`auth.signUp.errors.${key}`) : null
})

onMounted(async () => {
  const result = await api.organizations.list()
  organizations.value = result.data ?? []
})

async function onSubmit() {
  if (!auth.isEmailVerified) {
    error.value = t('repo.create.emailRequired')
    return
  }

  const key = validateSlug(slug.value)
  if (key) {
    slugError.value = t(`auth.signUp.errors.${key}`)
    return
  }

  loading.value = true
  error.value = null
  try {
    const result = await api.repositories.create(slug.value, {
      repositoryName: name.value,
      isPrivate: isPrivate.value,
    })
    if (result.error) {
      error.value = result.error
      return
    }
    const owner = ownerType.value === 'organization' && ownerOrg.value
      ? ownerOrg.value
      : auth.user?.username ?? 'me'
    await navigateTo(`/${owner}/${slug.value}`)
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
        {{ t('repo.create.title') }}
      </h1>
      <p class="mt-1 text-sm text-[var(--ogb-text-muted)]">
        {{ t('repo.create.subtitle') }}
      </p>
    </div>

    <UCard>
      <form
        class="space-y-4"
        @submit.prevent="onSubmit"
      >
        <UFormField
          :label="t('repo.create.nameLabel')"
          required
        >
          <UInput v-model="name" />
        </UFormField>

        <UFormField
          :label="t('repo.create.slugLabel')"
          :error="slugError ?? undefined"
          required
        >
          <UInput
            v-model="slug"
            @input="slugTouched = true"
          />
        </UFormField>

        <UFormField :label="t('repo.create.ownerLabel')">
          <USelect
            v-model="ownerType"
            :items="[
              { label: t('repo.create.ownerUser'), value: 'user' },
              { label: t('repo.create.ownerOrg'), value: 'organization' },
            ]"
          />
        </UFormField>

        <UFormField
          v-if="ownerType === 'organization'"
          :label="t('repo.create.orgLabel')"
        >
          <USelect
            v-model="ownerOrg"
            :items="organizations.map(o => ({ label: o.name, value: o.name }))"
            :placeholder="t('repo.create.orgPlaceholder')"
          />
        </UFormField>

        <UCheckbox
          v-model="isPrivate"
          :label="t('repo.create.privateLabel')"
        />

        <UAlert
          v-if="error"
          color="error"
          variant="subtle"
          :description="error"
        />

        <UButton
          type="submit"
          :loading="loading"
          :disabled="!!slugError || !auth.isEmailVerified"
        >
          {{ t('repo.create.submit') }}
        </UButton>
      </form>
    </UCard>
  </div>
</template>
