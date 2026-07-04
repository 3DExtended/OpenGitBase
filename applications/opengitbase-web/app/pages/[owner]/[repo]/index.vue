<script setup lang="ts">
import type {
  RepositoryContentReadme,
  RepositoryContentRefs,
  RepositoryContentTree,
  RepositoryReplicationLag,
} from '~/utils/api'

const { t } = useI18n()
const api = useApi()
const { owner, repoSlug, repo, loading, notFound, loadRepo } = useRepoMetadata()

const contentRefs = ref<RepositoryContentRefs | null>(null)
const tree = ref<RepositoryContentTree | null>(null)
const readme = ref<RepositoryContentReadme | null>(null)
const selectedRef = ref('')
const contentLoading = ref(false)
const contentForbidden = ref(false)
const contentUnavailable = ref(false)
const contentInitialized = ref(false)

const isEmpty = computed(() => contentRefs.value?.isEmpty ?? false)

const replicationLag = computed<RepositoryReplicationLag | null>(() =>
  tree.value?.replicationLag
  ?? readme.value?.replicationLag
  ?? contentRefs.value?.replicationLag
  ?? null,
)

async function loadRefs(): Promise<void> {
  contentForbidden.value = false
  contentUnavailable.value = false

  const refsResult = await api.repositoryContent.getRefs(owner.value, repoSlug.value)
  if (refsResult.status === 403) {
    contentForbidden.value = true
    return
  }
  if (refsResult.status === 503) {
    contentUnavailable.value = true
    return
  }
  if (!refsResult.data) {
    return
  }

  contentRefs.value = refsResult.data
  if (!selectedRef.value && refsResult.data.defaultRef) {
    selectedRef.value = refsResult.data.defaultRef
  }
}

async function loadBrowseContent(): Promise<void> {
  if (!selectedRef.value || contentRefs.value?.isEmpty) {
    tree.value = null
    readme.value = null
    return
  }

  const [treeResult, readmeResult] = await Promise.all([
    api.repositoryContent.getTree(owner.value, repoSlug.value, selectedRef.value, ''),
    api.repositoryContent.getReadme(owner.value, repoSlug.value, selectedRef.value),
  ])

  tree.value = treeResult.data
  readme.value = readmeResult.status === 404 ? null : readmeResult.data
}

async function loadContent(): Promise<void> {
  contentLoading.value = true
  await loadRefs()
  await loadBrowseContent()
  contentLoading.value = false
  contentInitialized.value = true
}

watch(selectedRef, async (next, previous) => {
  if (!contentInitialized.value || !next || next === previous) {
    return
  }
  contentLoading.value = true
  await loadBrowseContent()
  contentLoading.value = false
})

useHead({
  title: computed(() => repo.value ? `${owner.value}/${repoSlug.value}` : t('repo.overview.title')),
})

onMounted(async () => {
  await loadRepo()
  if (repo.value) {
    await loadContent()
  }
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

      <RepoSyncBanner :lag="replicationLag" />
      <RepoMergeRequestBanner
        v-if="selectedRef"
        :owner="owner"
        :repo-slug="repoSlug"
        :ref-name="selectedRef"
      />

      <UCard v-if="contentForbidden">
        <p class="text-sm text-[var(--ogb-text-muted)]">
          {{ t('repo.browse.forbidden') }}
        </p>
      </UCard>

      <UCard v-else-if="contentUnavailable">
        <p class="text-sm text-[var(--ogb-text-muted)]">
          {{ t('repo.browse.unavailable') }}
        </p>
      </UCard>

      <template v-else>
        <UCard v-if="isEmpty">
          <h2 class="font-semibold">
            {{ t('repo.browse.emptyTitle') }}
          </h2>
          <p class="mt-2 text-sm text-[var(--ogb-text-muted)]">
            {{ t('repo.browse.emptyDescription') }}
          </p>
        </UCard>

        <template v-else-if="contentRefs">
          <UCard>
            <div class="space-y-4">
              <RepoRefPicker
                v-model="selectedRef"
                :owner="owner"
                :repo="repoSlug"
                :branches="contentRefs.branches"
                :tags="contentRefs.tags"
              />

              <div
                v-if="contentLoading"
                class="text-sm text-[var(--ogb-text-muted)]"
              >
                {{ t('common.loading') }}
              </div>

              <RepoDirectoryTable
                v-else-if="tree"
                :owner="owner"
                :repo="repoSlug"
                :ref-name="selectedRef"
                :entries="tree.entries"
              />
            </div>
          </UCard>

          <UCard v-if="readme && !contentLoading">
            <template #header>
              <h2 class="font-semibold">
                {{ readme.fileName }}
              </h2>
            </template>
            <RepoMarkdown :source="readme.markdownSource" />
          </UCard>
        </template>
      </template>

      <RepoCloneCollapsible
        :owner="owner"
        :repo-slug="repoSlug"
        :default-open="isEmpty"
      />
    </template>
  </div>
</template>
