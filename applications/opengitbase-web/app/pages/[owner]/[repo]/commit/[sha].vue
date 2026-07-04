<script setup lang="ts">
import type { RepositoryCommit } from '~/utils/api'
import { repoBlobPath, repoHomePath, repoTreePath } from '~/utils/repoBrowse'

const route = useRoute()
const router = useRouter()
const { t } = useI18n()
const api = useApi()
const toast = useToast()

const owner = computed(() => String(route.params.owner))
const repoSlug = computed(() => String(route.params.repo))
const shaParam = computed(() => String(route.params.sha))

const commit = ref<RepositoryCommit | null>(null)
const loading = ref(true)
const error = ref<string | null>(null)

const backLink = computed(() => {
  const from = String(route.query.from ?? '')
  if (from.startsWith('mr/')) {
    const mrNumber = from.slice(3)
    return {
      label: t('repo.commit.backToMr', { number: mrNumber }),
      to: `/${owner.value}/${repoSlug.value}/merge-requests/${mrNumber}`,
    }
  }
  if (from.startsWith('discussions/')) {
    const discussionNumber = from.slice('discussions/'.length)
    return {
      label: t('repo.commit.backToDiscussion', { number: discussionNumber }),
      to: `/${owner.value}/${repoSlug.value}/discussions/${discussionNumber}`,
    }
  }
  return null
})

async function loadCommit(): Promise<void> {
  loading.value = true
  error.value = null
  const result = await api.repositoryContent.getCommit(owner.value, repoSlug.value, shaParam.value)
  if (result.status === 404) {
    error.value = t('repo.commit.notFound')
    commit.value = null
    loading.value = false
    return
  }
  if (result.error || !result.data) {
    error.value = result.error ?? t('repo.commit.notFound')
    commit.value = null
    loading.value = false
    return
  }
  commit.value = result.data
  if (result.data.sha !== shaParam.value) {
    await router.replace({
      params: {
        owner: owner.value,
        repo: repoSlug.value,
        sha: result.data.sha,
      },
      query: route.query,
    })
  }
  loading.value = false
}

async function copySha(): Promise<void> {
  if (!commit.value) {
    return
  }
  await navigator.clipboard.writeText(commit.value.sha)
  toast.add({ title: t('repo.commit.copiedSha'), color: 'success' })
}

watch(shaParam, () => {
  void loadCommit()
})

onMounted(() => {
  void loadCommit()
})
</script>

<template>
  <div class="mx-auto max-w-7xl space-y-4">
    <div class="flex flex-wrap items-center gap-2">
      <UButton
        v-if="backLink"
        :to="backLink.to"
        variant="ghost"
        icon="i-lucide-arrow-left"
        size="sm"
      >
        {{ backLink.label }}
      </UButton>
      <UButton
        v-else
        :to="repoHomePath(owner, repoSlug)"
        variant="ghost"
        icon="i-lucide-arrow-left"
        size="sm"
      >
        {{ owner }}/{{ repoSlug }}
      </UButton>
    </div>

    <UCard v-if="loading || error || !commit">
      <p
        v-if="loading"
        class="text-sm text-[var(--ogb-text-muted)]"
      >
        {{ t('common.loading') }}
      </p>
      <p
        v-else
        class="text-sm text-[var(--ogb-text-muted)]"
      >
        {{ error ?? t('repo.commit.notFound') }}
      </p>
    </UCard>

    <template v-else>
      <header class="space-y-3">
        <p class="font-mono text-sm text-[var(--ogb-text-muted)]">
          {{ commit.shortSha }}
        </p>
        <h1 class="whitespace-pre-wrap text-2xl font-semibold">
          {{ commit.message }}
        </h1>
        <div class="flex flex-wrap items-center gap-x-3 gap-y-1 text-sm text-[var(--ogb-text-muted)]">
          <span>{{ commit.authorName }}</span>
          <RelativeTime :iso="commit.authoredAt" />
          <span v-if="commit.stats">
            {{
              t('repo.commit.stats', {
                files: commit.stats.filesChanged,
                insertions: commit.stats.insertions,
                deletions: commit.stats.deletions,
              })
            }}
          </span>
        </div>
        <div
          v-if="commit.parents.length"
          class="flex flex-wrap items-center gap-2 text-sm"
        >
          <span class="text-[var(--ogb-text-muted)]">{{ t('repo.commit.parents') }}</span>
          <RepoCommitLink
            v-for="parent in commit.parents"
            :key="parent.sha"
            :owner="owner"
            :repo="repoSlug"
            :sha="parent.sha"
            :from="String(route.query.from ?? '') || undefined"
          />
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton
            variant="soft"
            icon="i-lucide-folder-tree"
            :to="repoTreePath(owner, repoSlug, commit.sha)"
          >
            {{ t('repo.commit.browseFiles') }}
          </UButton>
          <UButton
            variant="ghost"
            icon="i-lucide-copy"
            @click="copySha"
          >
            {{ t('repo.commit.copySha') }}
          </UButton>
        </div>
      </header>

      <RepoUnifiedDiff
        v-if="commit.kind === 'diff'"
        :files="commit.diffFiles"
        read-only
        :empty-label="t('repo.commit.emptyDiff')"
      />

      <UCard v-else>
        <ul class="divide-y" style="border-color: var(--ogb-border);">
          <li
            v-for="file in commit.rootFiles"
            :key="file.path"
            class="py-2"
          >
            <NuxtLink
              :to="repoBlobPath(owner, repoSlug, commit.sha, file.path)"
              class="font-mono text-sm text-[var(--ogb-accent)] hover:underline"
            >
              {{ file.path }}
            </NuxtLink>
          </li>
        </ul>
      </UCard>
    </template>
  </div>
</template>
