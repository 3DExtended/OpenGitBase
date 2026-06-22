<script setup lang="ts">
import type {
  RepositoryContentBlob,
  RepositoryContentRefs,
  RepositoryReplicationLag,
} from '~/utils/api'
import {
  decodeRefParam,
  fileNameFromPath,
  parsePathParam,
  repoHomePath,
  repoTreePath,
} from '~/utils/repoBrowse'

const route = useRoute()
const { t } = useI18n()
const api = useApi()
const { owner, repoSlug, repo, loading, notFound, loadRepo } = useRepoMetadata()

const contentRefs = ref<RepositoryContentRefs | null>(null)
const blob = ref<RepositoryContentBlob | null>(null)
const contentLoading = ref(false)
const contentForbidden = ref(false)
const contentUnavailable = ref(false)
const blobNotFound = ref(false)

const refName = computed(() => decodeRefParam(String(route.params.ref)))
const currentPath = computed(() => parsePathParam(route.params.path))

const rawUrl = computed(() =>
  currentPath.value
    ? api.repositoryContent.getRawBlobUrl(owner.value, repoSlug.value, refName.value, currentPath.value)
    : '',
)

const replicationLag = computed<RepositoryReplicationLag | null>(() =>
  blob.value?.replicationLag ?? contentRefs.value?.replicationLag ?? null,
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
  for (let index = 0; index < segments.length - 1; index += 1) {
    const segment = segments[index]!
    accumulated = accumulated ? `${accumulated}/${segment}` : segment
    items.push({
      label: segment,
      to: repoTreePath(owner.value, repoSlug.value, refName.value, accumulated),
    })
  }

  items.push({ label: fileNameFromPath(currentPath.value) })
  return items
})

async function loadBlob(): Promise<void> {
  if (!currentPath.value) {
    blobNotFound.value = true
    return
  }

  contentLoading.value = true
  blobNotFound.value = false
  contentForbidden.value = false
  contentUnavailable.value = false

  const refsResult = await api.repositoryContent.getRefs(owner.value, repoSlug.value)
  if (refsResult.status === 403) {
    contentForbidden.value = true
    contentLoading.value = false
    return
  }
  if (refsResult.status === 503) {
    contentUnavailable.value = true
    contentLoading.value = false
    return
  }
  contentRefs.value = refsResult.data

  const blobResult = await api.repositoryContent.getBlob(
    owner.value,
    repoSlug.value,
    refName.value,
    currentPath.value,
  )

  if (blobResult.status === 403) {
    contentForbidden.value = true
  }
  else if (blobResult.status === 503) {
    contentUnavailable.value = true
  }
  else if (blobResult.status === 404) {
    blobNotFound.value = true
  }
  else {
    blob.value = blobResult.data
  }

  contentLoading.value = false
}

watch(
  () => [route.params.ref, route.params.path] as const,
  () => {
    loadBlob()
  },
)

useHead({
  title: computed(() => {
    const fileName = currentPath.value ? fileNameFromPath(currentPath.value) : refName.value
    return `${owner.value}/${repoSlug.value} · ${fileName}`
  }),
})

onMounted(async () => {
  await loadRepo()
  if (repo.value) {
    await loadBlob()
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

      <UCard v-else-if="blobNotFound">
        <p class="text-sm text-[var(--ogb-text-muted)]">
          {{ t('repo.browse.fileNotFound') }}
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

          <div
            v-if="contentLoading"
            class="text-sm text-[var(--ogb-text-muted)]"
          >
            {{ t('common.loading') }}
          </div>

          <RepoBlobViewer
            v-else-if="blob"
            :blob="blob"
            :raw-url="rawUrl"
          />
        </div>
      </UCard>
    </template>
  </div>
</template>
