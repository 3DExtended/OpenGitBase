<script setup lang="ts">
import type { BlockedUser, RepositoryMember } from '~/utils/api'

definePageMeta({ middleware: 'auth' })

const route = useRoute()
const auth = useAuth()
const { t } = useI18n()
const api = useApi()

const owner = computed(() => String(route.params.owner))
const repoSlug = computed(() => String(route.params.repo))

const members = ref<RepositoryMember[]>([])
const blockedUsers = ref<BlockedUser[]>([])
const loading = ref(true)
const forbidden = ref(false)

const blockUserId = ref('')
const blockReason = ref('')
const blocking = ref(false)
const blockError = ref<string | null>(null)

const currentMemberRole = computed(() => {
  const member = members.value.find(m => m.username === auth.user?.username)
  return member?.role ?? 0
})

const isAdminPlus = computed(() => currentMemberRole.value >= 3)

useHead({ title: t('repo.discussions.settings.title') })

async function load(): Promise<void> {
  loading.value = true
  forbidden.value = false

  const repoResult = await api.repositories.getBySlug(owner.value, repoSlug.value)
  const repo = repoResult.data
  if (!repo) {
    loading.value = false
    return
  }

  const membersResult = await api.repositoryMembers.list(repo.id)
  members.value = membersResult.data ?? []

  if (!isAdminPlus.value) {
    forbidden.value = true
    loading.value = false
    return
  }

  const blockedResult = await api.discussions.blockedUsers.list(owner.value, repoSlug.value)
  if (blockedResult.status === 403) {
    forbidden.value = true
  }
  else {
    blockedUsers.value = blockedResult.data ?? []
  }
  loading.value = false
}

async function blockUser(): Promise<void> {
  blocking.value = true
  blockError.value = null
  try {
    const result = await api.discussions.blockedUsers.block(owner.value, repoSlug.value, {
      userId: blockUserId.value.trim(),
      reason: blockReason.value.trim() || null,
    })
    if (result.error) {
      blockError.value = result.error
      return
    }
    blockUserId.value = ''
    blockReason.value = ''
    const blockedResult = await api.discussions.blockedUsers.list(owner.value, repoSlug.value)
    blockedUsers.value = blockedResult.data ?? []
  }
  finally {
    blocking.value = false
  }
}

async function unblockUser(userId: string): Promise<void> {
  await api.discussions.blockedUsers.unblock(owner.value, repoSlug.value, userId)
  const blockedResult = await api.discussions.blockedUsers.list(owner.value, repoSlug.value)
  blockedUsers.value = blockedResult.data ?? []
}

onMounted(() => {
  void load()
})
</script>

<template>
  <div class="mx-auto max-w-2xl space-y-6">
    <UButton
      :to="`/${owner}/${repoSlug}/settings`"
      variant="ghost"
      icon="i-lucide-arrow-left"
      size="sm"
    >
      {{ t('repo.settings.title') }}
    </UButton>

    <h1 class="text-2xl font-semibold">
      {{ t('repo.discussions.settings.title') }}
    </h1>

    <div
      v-if="loading"
      class="text-sm text-[var(--ogb-text-muted)]"
    >
      {{ t('common.loading') }}
    </div>

    <UCard v-else-if="forbidden">
      <p class="text-sm text-[var(--ogb-text-muted)]">
        {{ t('repo.discussions.settings.forbidden') }}
      </p>
    </UCard>

    <template v-else>
      <UCard>
        <template #header>
          <h2 class="font-semibold">
            {{ t('repo.discussions.settings.blockTitle') }}
          </h2>
        </template>
        <p class="mb-4 text-sm text-[var(--ogb-text-muted)]">
          {{ t('repo.discussions.settings.blockDescription') }}
        </p>
        <form
          class="space-y-4"
          @submit.prevent="blockUser"
        >
          <UFormField
            :label="t('repo.discussions.settings.userIdLabel')"
            required
          >
            <UInput
              v-model="blockUserId"
              :placeholder="t('repo.discussions.settings.userIdPlaceholder')"
            />
          </UFormField>
          <UFormField :label="t('repo.discussions.settings.reasonLabel')">
            <UInput v-model="blockReason" />
          </UFormField>
          <UAlert
            v-if="blockError"
            color="error"
            variant="subtle"
            :description="blockError"
          />
          <UButton
            type="submit"
            color="error"
            variant="soft"
            :loading="blocking"
          >
            {{ t('repo.discussions.settings.blockButton') }}
          </UButton>
        </form>
      </UCard>

      <UCard>
        <template #header>
          <h2 class="font-semibold">
            {{ t('repo.discussions.settings.blockedListTitle') }}
          </h2>
        </template>

        <p
          v-if="!blockedUsers.length"
          class="text-sm text-[var(--ogb-text-muted)]"
        >
          {{ t('repo.discussions.settings.blockedEmpty') }}
        </p>

        <ul
          v-else
          class="divide-y"
          style="border-color: var(--ogb-border);"
        >
          <li
            v-for="blocked in blockedUsers"
            :key="blocked.userId"
            class="flex items-center justify-between gap-4 py-3 first:pt-0 last:pb-0"
          >
            <div class="min-w-0">
              <p class="font-medium">
                {{ blocked.username ?? blocked.userId }}
              </p>
              <p
                v-if="blocked.reason"
                class="text-xs text-[var(--ogb-text-muted)]"
              >
                {{ blocked.reason }}
              </p>
              <p class="text-xs text-[var(--ogb-text-muted)]">
                {{ new Date(blocked.blockedAt).toLocaleString() }}
              </p>
            </div>
            <UButton
              variant="ghost"
              size="sm"
              @click="unblockUser(blocked.userId)"
            >
              {{ t('repo.discussions.settings.unblockButton') }}
            </UButton>
          </li>
        </ul>
      </UCard>
    </template>
  </div>
</template>
