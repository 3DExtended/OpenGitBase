<script setup lang="ts">
const props = defineProps<{
  owner: string
  repoSlug: string
  defaultOpen?: boolean
}>()

const { t } = useI18n()
const { config: gitConfig, load: loadGitConfig } = useGitConfig()

const open = ref(props.defaultOpen ?? false)

onMounted(() => {
  loadGitConfig()
})

watch(
  () => props.defaultOpen,
  (value) => {
    if (value != null) {
      open.value = value
    }
  },
)

const httpsCloneUrl = computed(() =>
  gitConfig.value
    ? `${gitConfig.value.gitBaseUrl.replace(/\/$/, '')}/${props.owner}/${props.repoSlug}.git`
    : '',
)

const sshCloneUrl = computed(() => {
  if (!gitConfig.value?.sshEnabled) {
    return ''
  }
  try {
    const host = new URL(gitConfig.value.gitBaseUrl).hostname
    return `git@${host}:${props.owner}/${props.repoSlug}.git`
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
</script>

<template>
  <UCard>
    <UCollapsible v-model:open="open">
      <button
        type="button"
        class="flex w-full items-center justify-between gap-2 text-left"
      >
        <h2 class="font-semibold">
          {{ t('repo.browse.cloneSectionTitle') }}
        </h2>
        <span
          class="text-sm text-[var(--ogb-text-muted)]"
          aria-hidden="true"
        >
          {{ open ? '▾' : '▸' }}
        </span>
      </button>

      <template #content>
        <div class="mt-6 space-y-6">
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

          <section>
            <h3 class="font-medium">
              {{ t('repo.overview.pushSectionTitle') }}
            </h3>
            <p class="mt-1 text-sm text-[var(--ogb-text-muted)]">
              {{ t('repo.overview.pushSectionIntro') }}
            </p>
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
            <ol
              v-if="sshCloneUrl"
              class="mt-6 list-decimal space-y-4 pl-5 text-sm"
            >
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
            <p class="mt-4 text-sm text-[var(--ogb-text-muted)]">
              {{ t('repo.overview.branchHint') }}
            </p>
          </section>
        </div>
      </template>
    </UCollapsible>
  </UCard>
</template>
