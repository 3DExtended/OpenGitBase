<script setup lang="ts">
import type { PipelineRun, RepositoryCommit, RepositoryReplicationLag } from '~/utils/api'
import { resolveCommitPageLoad } from '~/utils/commitPageLoad'
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
const forbidden = ref(false)
const unavailable = ref(false)
const pipelineRun = ref<PipelineRun | null>(null)

let loadSequence = 0

const replicationLag = computed<RepositoryReplicationLag | null>(() =>
  commit.value?.replicationLag ?? null,
)

const pipelineLabel = computed(() => {
  if (!pipelineRun.value) {
    return null
  }
  switch (pipelineRun.value.status) {
    case 'Queued':
      return t('repo.commit.pipelineQueued')
    case 'Running':
      return t('repo.commit.pipelineRunning')
    case 'Passed':
      return t('repo.commit.pipelinePassed')
    case 'Failed':
      return t('repo.commit.pipelineFailed')
    case 'Cancelled':
      return t('repo.commit.pipelineCancelled')
  }
})

const pipelineColor = computed<'neutral' | 'info' | 'success' | 'warning' | 'error'>(() => {
  switch (pipelineRun.value?.status) {
    case 'Running':
      return 'info'
    case 'Passed':
      return 'success'
    case 'Cancelled':
      return 'warning'
    case 'Failed':
      return 'error'
    default:
      return 'neutral'
  }
})

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
  const sequence = ++loadSequence
  loading.value = true
  error.value = null
  forbidden.value = false
  unavailable.value = false
  commit.value = null
  pipelineRun.value = null

  const result = await api.repositoryContent.getCommit(owner.value, repoSlug.value, shaParam.value)
  if (sequence !== loadSequence) {
    return
  }

  const resolved = resolveCommitPageLoad(result, {
    notFound: t('repo.commit.notFound'),
    generic: t('repo.commit.notFound'),
  })
  forbidden.value = resolved.forbidden
  unavailable.value = resolved.unavailable
  error.value = resolved.error
  commit.value = resolved.commit

  if (resolved.commit) {
    await loadPipelineForCommit(resolved.commit.sha)
  }

  if (resolved.commit && resolved.commit.sha !== shaParam.value) {
    await router.replace({
      path: `/${owner.value}/${repoSlug.value}/commit/${resolved.commit.sha}`,
      query: route.query,
    })
  }

  if (sequence !== loadSequence) {
    return
  }

  loading.value = false
}

async function loadPipelineForCommit(sha: string): Promise<void> {
  const runsResult = await api.pipelines.list(owner.value, repoSlug.value)
  if (runsResult.error || !runsResult.data) {
    return
  }
  pipelineRun.value = runsResult.data.find(run => run.afterSha === sha) ?? null
}

async function copySha(): Promise<void> {
  if (!commit.value) {
    return
  }
  try {
    await navigator.clipboard.writeText(commit.value.sha)
    toast.add({ title: t('repo.commit.copiedSha'), color: 'success' })
  }
  catch {
    toast.add({ title: t('repo.commit.copyShaFailed'), color: 'error' })
  }
}

watch([owner, repoSlug, shaParam], () => {
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

    <UCard v-if="loading || forbidden || unavailable || error || !commit">
      <p
        v-if="loading"
        class="text-sm text-[var(--ogb-text-muted)]"
      >
        {{ t('common.loading') }}
      </p>
      <p
        v-else-if="forbidden"
        class="text-sm text-[var(--ogb-text-muted)]"
      >
        {{ t('repo.browse.forbidden') }}
      </p>
      <p
        v-else-if="unavailable"
        class="text-sm text-[var(--ogb-text-muted)]"
      >
        {{ t('repo.browse.unavailable') }}
      </p>
      <p
        v-else
        class="text-sm text-[var(--ogb-text-muted)]"
      >
        {{ error ?? t('repo.commit.notFound') }}
      </p>
    </UCard>

    <template v-else>
      <RepoSyncBanner :lag="replicationLag" />

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
          <NuxtLink
            v-if="pipelineRun && pipelineLabel"
            :to="`/${owner}/${repoSlug}/pipelines/${pipelineRun.id}`"
            class="inline-flex"
          >
            <CollaborationStatusBadge
              :label="pipelineLabel"
              :color="pipelineColor"
            />
          </NuxtLink>
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
        <p
          v-if="!commit.rootFiles.length"
          class="text-sm text-[var(--ogb-text-muted)]"
        >
          {{ t('repo.commit.emptyRootFiles') }}
        </p>
        <ul
          v-else
          class="divide-y"
          style="border-color: var(--ogb-border);"
        >
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
