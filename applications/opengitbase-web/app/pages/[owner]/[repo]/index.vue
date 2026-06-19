<script setup lang="ts">
import type { Repository } from '~/utils/api'

const route = useRoute()
const { t } = useI18n()
const api = useApi()
const { config: gitConfig, load: loadGitConfig } = useGitConfig()

const owner = computed(() => String(route.params.owner))
const repoSlug = computed(() => String(route.params.repo))

const repo = ref<Repository | null>(null)
const loading = ref(true)
const notFound = ref(false)

const httpsCloneUrl = computed(() =>
  repo.value && gitConfig.value
    ? `${gitConfig.value.gitBaseUrl.replace(/\/$/, '')}/${owner.value}/${repoSlug.value}.git`
    : '',
)

const sshCloneUrl = computed(() => {
  if (!repo.value || !gitConfig.value?.sshEnabled) {
    return ''
  }
  try {
    const host = new URL(gitConfig.value.gitBaseUrl).hostname
    return `git@${host}:${owner.value}/${repoSlug.value}.git`
  }
  catch {
    return ''
  }
})

function withHttpsAuth(url: string, tokenPlaceholder = 'YOUR_TOKEN'): string {
  try {
    const parsed = new URL(url)
    parsed.username = 'git'
    parsed.password = tokenPlaceholder
    return parsed.toString()
  }
  catch {
    return url
  }
}

const httpsCloneWithTokenUrl = computed(() =>
  httpsCloneUrl.value ? withHttpsAuth(httpsCloneUrl.value) : '',
)

const pushExistingHttpsCommands = computed(() => {
  if (!httpsCloneWithTokenUrl.value) {
    return ''
  }
  return [
    'cd path/to/your-repo',
    `git remote add origin ${httpsCloneWithTokenUrl.value}`,
    'git branch -M main',
    'git push -u origin main',
  ].join('\n')
})

const pushExistingSshCommands = computed(() => {
  if (!sshCloneUrl.value) {
    return ''
  }
  return [
    'cd path/to/your-repo',
    `git remote add origin ${sshCloneUrl.value}`,
    'git branch -M main',
    'git push -u origin main',
  ].join('\n')
})

useHead({
  title: computed(() => repo.value ? `${owner.value}/${repoSlug.value}` : t('repo.overview.title')),
})

onMounted(async () => {
  loading.value = true
  await loadGitConfig()
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
            {{ t('repo.overview.cloneSectionTitle') }}
          </h2>
        </template>

        <div class="space-y-6">
          <section>
            <h3 class="font-medium">
              {{ t('repo.overview.httpsCloneTitle') }}
            </h3>
            <p class="mt-1 text-sm text-[var(--ogb-text-muted)]">
              {{ t('repo.overview.httpsHint') }}
              <NuxtLink
                to="/settings/access-tokens"
                class="text-[var(--ogb-accent)] hover:underline"
              >
                {{ t('settings.accessTokens.link') }}
              </NuxtLink>
            </p>
            <code class="mt-3 block rounded-md bg-[var(--ogb-bg)] px-3 py-2 font-mono text-sm break-all">
              {{ httpsCloneUrl }}
            </code>
            <ol class="mt-4 list-decimal space-y-4 pl-5 text-sm">
              <li>
                {{ t('repo.overview.cloneHttpsStep1BeforeLink') }}
                <NuxtLink
                  to="/settings/access-tokens"
                  class="text-[var(--ogb-accent)] hover:underline"
                >
                  {{ t('repo.overview.cloneHttpsStep1Link') }}
                </NuxtLink>
                {{ t('repo.overview.cloneHttpsStep1AfterLink') }}
              </li>
              <li>
                <p>{{ t('repo.overview.cloneHttpsStep2') }}</p>
                <code class="mt-2 block whitespace-pre-wrap rounded-md bg-[var(--ogb-bg)] px-3 py-2 font-mono text-sm break-all">
                  git clone {{ httpsCloneWithTokenUrl }}
                </code>
                <p class="mt-2 text-[var(--ogb-text-muted)]">
                  {{ t('repo.overview.cloneHttpsPromptHint', { username: 'git' }) }}
                </p>
              </li>
            </ol>
          </section>

          <section v-if="sshCloneUrl">
            <h3 class="font-medium">
              {{ t('repo.overview.sshCloneTitle') }}
            </h3>
            <p class="mt-1 text-sm text-[var(--ogb-text-muted)]">
              {{ t('repo.overview.sshHint') }}
              <NuxtLink
                to="/settings/ssh-keys"
                class="text-[var(--ogb-accent)] hover:underline"
              >
                {{ t('settings.sshKeys.link') }}
              </NuxtLink>
            </p>
            <code class="mt-3 block rounded-md bg-[var(--ogb-bg)] px-3 py-2 font-mono text-sm break-all">
              {{ sshCloneUrl }}
            </code>
            <ol class="mt-4 list-decimal space-y-4 pl-5 text-sm">
              <li>
                {{ t('repo.overview.cloneSshStep1BeforeLink') }}
                <NuxtLink
                  to="/settings/ssh-keys"
                  class="text-[var(--ogb-accent)] hover:underline"
                >
                  {{ t('repo.overview.cloneSshStep1Link') }}
                </NuxtLink>
                {{ t('repo.overview.cloneSshStep1AfterLink') }}
              </li>
              <li>
                <p>{{ t('repo.overview.cloneSshStep2') }}</p>
                <code class="mt-2 block whitespace-pre-wrap rounded-md bg-[var(--ogb-bg)] px-3 py-2 font-mono text-sm break-all">
                  git clone {{ sshCloneUrl }}
                </code>
              </li>
            </ol>
          </section>
        </div>
      </UCard>

      <UCard>
        <template #header>
          <h2 class="font-semibold">
            {{ t('repo.overview.pushSectionTitle') }}
          </h2>
        </template>
        <p class="text-sm text-[var(--ogb-text-muted)]">
          {{ t('repo.overview.pushSectionIntro') }}
        </p>

        <div class="mt-6 space-y-6">
          <section>
            <h3 class="font-medium">
              {{ t('repo.overview.httpsCloneTitle') }}
            </h3>
            <ol class="mt-4 list-decimal space-y-4 pl-5 text-sm">
              <li>
                {{ t('repo.overview.pushHttpsStep1BeforeLink') }}
                <NuxtLink
                  to="/settings/access-tokens"
                  class="text-[var(--ogb-accent)] hover:underline"
                >
                  {{ t('repo.overview.pushHttpsStep1Link') }}
                </NuxtLink>
                {{ t('repo.overview.pushHttpsStep1AfterLink') }}
              </li>
              <li>
                <p>{{ t('repo.overview.pushHttpsStep2') }}</p>
                <code class="mt-2 block whitespace-pre-wrap rounded-md bg-[var(--ogb-bg)] px-3 py-2 font-mono text-sm break-all">
                  {{ pushExistingHttpsCommands }}
                </code>
              </li>
            </ol>
          </section>

          <section v-if="sshCloneUrl">
            <h3 class="font-medium">
              {{ t('repo.overview.sshCloneTitle') }}
            </h3>
            <ol class="mt-4 list-decimal space-y-4 pl-5 text-sm">
              <li>
                {{ t('repo.overview.pushSshStep1BeforeLink') }}
                <NuxtLink
                  to="/settings/ssh-keys"
                  class="text-[var(--ogb-accent)] hover:underline"
                >
                  {{ t('repo.overview.pushSshStep1Link') }}
                </NuxtLink>
                {{ t('repo.overview.pushSshStep1AfterLink') }}
              </li>
              <li>
                <p>{{ t('repo.overview.pushSshStep2') }}</p>
                <code class="mt-2 block whitespace-pre-wrap rounded-md bg-[var(--ogb-bg)] px-3 py-2 font-mono text-sm break-all">
                  {{ pushExistingSshCommands }}
                </code>
              </li>
            </ol>
          </section>

          <p class="text-sm text-[var(--ogb-text-muted)]">
            {{ t('repo.overview.branchHint') }}
          </p>
        </div>
      </UCard>
    </template>
  </div>
</template>
