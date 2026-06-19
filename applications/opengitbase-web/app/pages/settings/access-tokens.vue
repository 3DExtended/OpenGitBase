<script setup lang="ts">
import type { GitAccessToken } from '~/utils/api'

definePageMeta({ middleware: 'auth' })

const { t } = useI18n()
const api = useApi()
const toast = useToast()

const tokens = ref<GitAccessToken[]>([])
const loading = ref(true)
const error = ref<string | null>(null)

const name = ref('')
const scope = ref('read')
const neverExpires = ref(false)
const adding = ref(false)
const addError = ref<string | null>(null)
const createdToken = ref<string | null>(null)

const scopeOptions = [
  { label: t('settings.accessTokens.scopeRead'), value: 'read' },
  { label: t('settings.accessTokens.scopeWrite'), value: 'write' },
]

useHead({ title: t('settings.accessTokens.title') })

async function loadTokens() {
  loading.value = true
  error.value = null
  try {
    const result = await api.accessTokens.list()
    if (result.error) {
      error.value = result.error
      return
    }
    tokens.value = result.data ?? []
  }
  finally {
    loading.value = false
  }
}

async function createToken() {
  adding.value = true
  addError.value = null
  createdToken.value = null
  try {
    const result = await api.accessTokens.create({
      name: name.value,
      scope: scope.value,
      neverExpires: neverExpires.value,
    })
    if (result.error || !result.data) {
      addError.value = result.error ?? 'Could not create token.'
      return
    }
    createdToken.value = result.data.token
    name.value = ''
    scope.value = 'read'
    neverExpires.value = false
    await loadTokens()
  }
  finally {
    adding.value = false
  }
}

async function revokeToken(id: string) {
  await api.accessTokens.delete(id)
  await loadTokens()
}

async function copyCreatedToken() {
  if (!createdToken.value) {
    return
  }
  await navigator.clipboard.writeText(createdToken.value)
  toast.add({ title: t('settings.accessTokens.copied'), color: 'success' })
}

function formatExpiry(token: GitAccessToken) {
  if (token.revokedAt) {
    return t('settings.accessTokens.revoked')
  }
  if (!token.expiresAt) {
    return t('settings.accessTokens.noExpiry')
  }
  return t('settings.accessTokens.expires', {
    date: new Date(token.expiresAt).toLocaleDateString(),
  })
}

onMounted(loadTokens)
</script>

<template>
  <div class="mx-auto max-w-2xl space-y-6">
    <div>
      <UButton
        to="/settings"
        variant="ghost"
        icon="i-lucide-arrow-left"
        size="sm"
        class="mb-4"
      >
        {{ t('settings.title') }}
      </UButton>
      <h1 class="text-2xl font-semibold">
        {{ t('settings.accessTokens.title') }}
      </h1>
      <p class="mt-1 text-sm text-[var(--ogb-text-muted)]">
        {{ t('settings.accessTokens.subtitle') }}
      </p>
    </div>

    <UAlert
      v-if="createdToken"
      color="primary"
      variant="subtle"
      :title="t('settings.accessTokens.createdTokenTitle')"
      :description="t('settings.accessTokens.createdTokenHint')"
    >
      <template #actions>
        <div class="flex w-full flex-col gap-2 sm:flex-row sm:items-center">
          <code class="flex-1 break-all rounded bg-[var(--ogb-surface)] px-2 py-1 text-xs">
            {{ createdToken }}
          </code>
          <UButton
            size="sm"
            @click="copyCreatedToken"
          >
            {{ t('settings.accessTokens.copyToken') }}
          </UButton>
        </div>
      </template>
    </UAlert>

    <UCard>
      <template #header>
        <h2 class="font-semibold">
          {{ t('settings.accessTokens.addTitle') }}
        </h2>
      </template>
      <form
        class="space-y-4"
        @submit.prevent="createToken"
      >
        <UFormField
          :label="t('settings.accessTokens.nameLabel')"
          required
        >
          <UInput
            v-model="name"
            required
          />
        </UFormField>
        <UFormField :label="t('settings.accessTokens.scopeLabel')">
          <USelect
            v-model="scope"
            :items="scopeOptions"
          />
        </UFormField>
        <UCheckbox
          v-model="neverExpires"
          :label="t('settings.accessTokens.neverExpires')"
        />
        <UAlert
          v-if="addError"
          color="error"
          variant="subtle"
          :description="addError"
        />
        <UButton
          type="submit"
          :loading="adding"
        >
          {{ t('settings.accessTokens.addButton') }}
        </UButton>
      </form>
    </UCard>

    <UCard>
      <template #header>
        <h2 class="font-semibold">
          {{ t('settings.accessTokens.listTitle') }}
        </h2>
      </template>
      <div
        v-if="loading"
        class="text-sm text-[var(--ogb-text-muted)]"
      >
        …
      </div>
      <UAlert
        v-else-if="error"
        color="error"
        variant="subtle"
        :description="error"
      />
      <p
        v-else-if="tokens.length === 0"
        class="text-sm text-[var(--ogb-text-muted)]"
      >
        {{ t('settings.accessTokens.empty') }}
      </p>
      <ul
        v-else
        class="divide-y divide-[var(--ogb-border)]"
      >
        <li
          v-for="token in tokens"
          :key="token.id"
          class="flex items-start justify-between gap-4 py-3"
        >
          <div>
            <p class="font-medium">
              {{ token.name }}
            </p>
            <p class="text-xs text-[var(--ogb-text-muted)]">
              {{ token.scope }} · {{ formatExpiry(token) }}
            </p>
          </div>
          <UButton
            v-if="!token.revokedAt"
            color="error"
            variant="ghost"
            size="sm"
            @click="revokeToken(token.id)"
          >
            Revoke
          </UButton>
        </li>
      </ul>
    </UCard>
  </div>
</template>
