<script setup lang="ts">
import type { Repository } from '~/utils/api'

const route = useRoute()
const { t } = useI18n()
const api = useApi()

const owner = computed(() => String(route.params.owner))
const repoSlug = computed(() => String(route.params.repo))

const repo = ref<Repository | null>(null)
const loading = ref(true)
const notFound = ref(false)

const requestUrl = useRequestURL()
const sshCloneUrl = computed(() =>
  repo.value ? `git@${requestUrl.hostname}:${owner.value}/${repoSlug.value}.git` : '',
)

useHead({
  title: computed(() => repo.value ? `${owner.value}/${repoSlug.value}` : t('repo.overview.title')),
})

onMounted(async () => {
  loading.value = true
  const result = await api.repositories.getBySlug(owner.value, repoSlug.value)
  if (!result.data) {
    const fallback = await api.repositories.list()
    const match = fallback.data?.find(r =>
      r.slug === repoSlug.value && (r.ownerSlug === owner.value || r.ownerUserId),
    )
    if (match) {
      repo.value = match
    }
    else {
      notFound.value = true
    }
  }
  else {
    repo.value = result.data
  }
  loading.value = false
})
</script>

<template>
  <div class="mx-auto max-w-4xl space-y-6">
    <div
      v-if="loading"
      class="text-sm text-[var(--ogb-text-muted)]"
    >
      {{ t('common.loading') }}
    </div>

    <UCard v-else-if="notFound">
      <p class="text-sm text-[var(--ogb-text-muted)]">
        {{ t('repo.notFound') }}
      </p>
    </UCard>

    <template v-else-if="repo">
      <div class="flex flex-wrap items-start justify-between gap-4">
        <div>
          <p class="font-mono text-sm text-[var(--ogb-text-muted)]">
            {{ owner }}/{{ repoSlug }}
          </p>
          <h1 class="mt-1 text-2xl font-semibold">
            {{ repo.name }}
          </h1>
        </div>
        <UBadge
          :color="repo.isPrivate ? 'neutral' : 'success'"
          variant="subtle"
        >
          {{ repo.isPrivate ? t('repo.visibility.private') : t('repo.visibility.public') }}
        </UBadge>
      </div>

      <div class="flex flex-wrap gap-2">
        <UButton
          :to="`/${owner}/${repoSlug}/settings`"
          variant="soft"
          size="sm"
        >
          {{ t('repo.settings.title') }}
        </UButton>
        <UButton
          :to="`/${owner}/${repoSlug}/members`"
          variant="soft"
          size="sm"
        >
          {{ t('repo.members.title') }}
        </UButton>
      </div>

      <UCard>
        <template #header>
          <h2 class="font-semibold">
            {{ t('repo.overview.cloneTitle') }}
          </h2>
        </template>
        <code class="block rounded-md bg-[var(--ogb-bg)] px-3 py-2 font-mono text-sm">
          {{ sshCloneUrl }}
        </code>
        <p class="mt-3 text-sm text-[var(--ogb-text-muted)]">
          {{ t('repo.overview.sshHint') }}
          <NuxtLink
            to="/settings/ssh-keys"
            class="text-[var(--ogb-accent)] hover:underline"
          >
            {{ t('settings.sshKeys.link') }}
          </NuxtLink>
        </p>
      </UCard>
    </template>
  </div>
</template>
