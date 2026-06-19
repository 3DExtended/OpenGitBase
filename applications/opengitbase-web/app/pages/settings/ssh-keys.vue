<script setup lang="ts">
import type { PublicGitSshKey } from '~/utils/api'

definePageMeta({ middleware: 'auth' })

const { t } = useI18n()
const api = useApi()
const { config: gitConfig, load: loadGitConfig } = useGitConfig()

const keys = ref<PublicGitSshKey[]>([])
const loading = ref(true)
const error = ref<string | null>(null)

const name = ref('')
const publicKey = ref('')
const adding = ref(false)
const addError = ref<string | null>(null)

useHead({ title: t('settings.sshKeys.title') })

async function loadKeys() {
  loading.value = true
  error.value = null
  try {
    const result = await api.sshKeys.list()
    if (result.error) {
      error.value = result.error
      return
    }
    keys.value = result.data ?? []
  }
  finally {
    loading.value = false
  }
}

async function addKey() {
  adding.value = true
  addError.value = null
  try {
    const result = await api.sshKeys.create({
      modelToCreate: {
        name: name.value,
        publicSSHKey: publicKey.value,
      },
    })
    if (result.error) {
      addError.value = result.error
      return
    }
    name.value = ''
    publicKey.value = ''
    await loadKeys()
  }
  finally {
    adding.value = false
  }
}

async function removeKey(id: string) {
  await api.sshKeys.delete(id)
  await loadKeys()
}

onMounted(async () => {
  await loadGitConfig()
  if (gitConfig.value?.sshEnabled) {
    await loadKeys()
  }
  else {
    loading.value = false
  }
})
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
        {{ t('settings.sshKeys.title') }}
      </h1>
      <p class="mt-1 text-sm text-[var(--ogb-text-muted)]">
        {{ t('settings.sshKeys.subtitle') }}
      </p>
    </div>

    <div
      v-if="!gitConfig"
      class="text-sm text-[var(--ogb-text-muted)]"
    >
      {{ t('common.loading') }}
    </div>

    <UCard v-else-if="!gitConfig.sshEnabled">
      <h2 class="font-semibold">
        {{ t('settings.sshKeys.disabledTitle') }}
      </h2>
      <p class="mt-2 text-sm text-[var(--ogb-text-muted)]">
        {{ t('settings.sshKeys.disabledHint') }}
      </p>
      <template #footer>
        <UButton
          to="/settings/access-tokens"
          variant="soft"
        >
          {{ t('settings.accessTokens.link') }}
        </UButton>
      </template>
    </UCard>

    <template v-else>
      <UCard>
        <template #header>
          <h2 class="font-semibold">
            {{ t('settings.sshKeys.addTitle') }}
          </h2>
        </template>
        <form
          class="space-y-4"
          @submit.prevent="addKey"
        >
          <UFormField
            :label="t('settings.sshKeys.nameLabel')"
            required
          >
            <UInput v-model="name" />
          </UFormField>
          <UFormField
            :label="t('settings.sshKeys.keyLabel')"
            required
          >
            <UTextarea
              v-model="publicKey"
              :rows="4"
              class="font-mono text-xs"
            />
          </UFormField>
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
            {{ t('settings.sshKeys.addButton') }}
          </UButton>
        </form>
      </UCard>

      <UCard>
      <template #header>
        <h2 class="font-semibold">
          {{ t('settings.sshKeys.listTitle') }}
        </h2>
      </template>

      <div
        v-if="loading"
        class="text-sm text-[var(--ogb-text-muted)]"
      >
        {{ t('common.loading') }}
      </div>

      <UAlert
        v-else-if="error"
        color="error"
        variant="subtle"
        :description="error"
      />

      <p
        v-else-if="!keys.length"
        class="text-sm text-[var(--ogb-text-muted)]"
      >
        {{ t('settings.sshKeys.empty') }}
      </p>

      <ul
        v-else
        class="divide-y"
        style="border-color: var(--ogb-border);"
      >
        <li
          v-for="key in keys"
          :key="key.id"
          class="flex items-start justify-between gap-4 py-4 first:pt-0 last:pb-0"
        >
          <div class="min-w-0">
            <p class="font-medium">
              {{ key.name }}
            </p>
            <p class="mt-1 font-mono text-xs text-[var(--ogb-text-muted)]">
              {{ key.fingerprint }}
            </p>
          </div>
          <UButton
            color="error"
            variant="ghost"
            size="sm"
            icon="i-lucide-trash-2"
            @click="removeKey(key.id)"
          />
        </li>
      </ul>
    </UCard>
    </template>
  </div>
</template>
