<script setup lang="ts">
import type {
  RepositoryContentRefs,
  RepositoryContentTree,
  RepositoryReplicationLag,
} from '~/utils/api'
import {
  decodeRefParam,
  parsePathParam,
  repoHomePath,
  repoTreePath,
} from '~/utils/repoBrowse'

const route = useRoute()
const router = useRouter()
const { t } = useI18n()
const api = useApi()
const { owner, repoSlug, repo, loading, notFound, loadRepo } = useRepoMetadata()

const contentRefs = ref<RepositoryContentRefs | null>(null)
const tree = ref<RepositoryContentTree | null>(null)
const contentLoading = ref(false)
const contentForbidden = ref(false)
const contentUnavailable = ref(false)
const treeNotFound = ref(false)

const refName = computed(() => decodeRefParam(String(route.params.ref)))
const currentPath = computed(() => parsePathParam(route.params.path))

const replicationLag = computed<RepositoryReplicationLag | null>(() =>
  tree.value?.replicationLag ?? contentRefs.value?.replicationLag ?? null,
)

const breadcrumbs = computed(() => {
  const items: Array<{ label: string, to?: string }> = [
    { label: `${owner.value}/${repoSlug.value}`, to: repoHomePath(owner.value, repoSlug.value) },
    { label: refName.value, to: repoTreePath(owner.value, repoSlug.value, refName.value) },
  ]

  if (!currentPath.value) {
    return items
  }

  const segments = currentPath.value.split('/')
  let accumulated = ''
  for (const segment of segments) {
    accumulated = accumulated ? `${accumulated}/${segment}` : segment
    items.push({
      label: segment,
      to: repoTreePath(owner.value, repoSlug.value, refName.value, accumulated),
    })
  }

  return items
})

async function loadRefs(): Promise<void> {
  const refsResult = await api.repositoryContent.getRefs(owner.value, repoSlug.value)
  if (refsResult.status === 403) {
    contentForbidden.value = true
    return
  }
  if (refsResult.status === 503) {
    contentUnavailable.value = true
    return
  }
  contentRefs.value = refsResult.data
}

async function loadTree(): Promise<void> {
  contentLoading.value = true
  treeNotFound.value = false
  contentForbidden.value = false
  contentUnavailable.value = false

  await loadRefs()
  if (contentForbidden.value || contentUnavailable.value) {
    contentLoading.value = false
    return
  }

  const treeResult = await api.repositoryContent.getTree(
    owner.value,
    repoSlug.value,
    refName.value,
    currentPath.value,
  )

  if (treeResult.status === 403) {
    contentForbidden.value = true
  }
  else if (treeResult.status === 503) {
    contentUnavailable.value = true
  }
  else if (treeResult.status === 404) {
    treeNotFound.value = true
  }
  else {
    tree.value = treeResult.data
  }

  contentLoading.value = false
}

function onRefChange(nextRef: string): void {
  router.push(repoTreePath(owner.value, repoSlug.value, nextRef, currentPath.value))
}

watch(
  () => [route.params.ref, route.params.path] as const,
  () => {
    loadTree()
  },
)

useHead({
  title: computed(() => {
    const pathLabel = currentPath.value || refName.value
    return `${owner.value}/${repoSlug.value} · ${pathLabel}`
  }),
})

onMounted(async () => {
  await loadRepo()
  if (repo.value) {
    await loadTree()
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
        :owner="owner"
        :repo-slug="repoSlug"
        :ref-name="refName"
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

      <UCard v-else-if="treeNotFound">
        <p class="text-sm text-[var(--ogb-text-muted)]">
          {{ t('repo.browse.pathNotFound') }}
        </p>
      </UCard>

      <UCard v-else>
        <div class="space-y-4">
          <nav
            aria-label="Breadcrumb"
            class="flex flex-wrap items-center gap-1 text-sm text-[var(--ogb-text-muted)]"
          >
            <template
              v-for="(item, index) in breadcrumbs"
              :key="`${item.label}-${index}`"
            >
              <span v-if="index > 0">/</span>
              <NuxtLink
                v-if="item.to && index < breadcrumbs.length - 1"
                :to="item.to"
                class="font-mono text-[var(--ogb-accent)] hover:underline"
              >
                {{ item.label }}
              </NuxtLink>
              <span
                v-else
                class="font-mono text-[var(--ogb-text)]"
              >
                {{ item.label }}
              </span>
            </template>
          </nav>

          <RepoRefPicker
            v-if="contentRefs"
            :model-value="refName"
            :branches="contentRefs.branches"
            :tags="contentRefs.tags"
            @update:model-value="onRefChange"
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
            :ref-name="refName"
            :entries="tree.entries"
          />
        </div>
      </UCard>
    </template>
  </div>
</template>
