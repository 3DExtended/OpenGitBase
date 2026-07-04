<script setup lang="ts">
import type { CommentAnchor } from '~/utils/api'
import { repoBlobPath } from '~/utils/repoBrowse'

const props = defineProps<{
  owner: string
  repoSlug: string
  anchor: CommentAnchor
  commitLinkFrom?: string
}>()

const api = useApi()
const { t } = useI18n()
const expanded = ref(true)
const snippet = ref<string | null>(null)
const snippetStartLine = ref(1)
const loading = ref(false)

const lineRange = computed(() => {
  const end = props.anchor.endLine ?? props.anchor.line
  return props.anchor.line === end
    ? `${props.anchor.line}`
    : `${props.anchor.line}–${end}`
})

const fileUrl = computed(() =>
  repoBlobPath(props.owner, props.repoSlug, props.anchor.ref, props.anchor.filePath),
)

const isOutdated = computed(() =>
  props.anchor.resolution?.kind && props.anchor.resolution.kind !== 'located',
)

async function loadSnippet(): Promise<void> {
  if (snippet.value || loading.value) {
    return
  }
  loading.value = true
  const result = await api.repositoryContent.getBlob(
    props.owner,
    props.repoSlug,
    props.anchor.ref,
    props.anchor.filePath,
  )
  const lines = result.data?.textContent?.split('\n') ?? []
  const start = Math.max(1, props.anchor.line)
  const end = Math.max(start, props.anchor.endLine ?? props.anchor.line)
  const from = Math.max(0, start - 1)
  const to = Math.min(lines.length, end)
  const contextStart = Math.max(0, from - 2)
  const contextEnd = Math.min(lines.length, to + 2)
  snippetStartLine.value = contextStart + 1
  snippet.value = lines.slice(contextStart, contextEnd).join('\n')
  loading.value = false
}

watch(expanded, (isExpanded) => {
  if (isExpanded) {
    void loadSnippet()
  }
}, { immediate: true })
</script>

<template>
  <div
    class="rounded-lg border text-sm"
    style="border-color: var(--ogb-border); background: var(--ogb-bg);"
  >
    <button
      type="button"
      class="flex w-full items-center justify-between gap-2 px-3 py-2 text-left"
      @click="expanded = !expanded"
    >
      <span class="min-w-0 truncate font-mono text-xs text-[var(--ogb-text-muted)]">
        {{ anchor.filePath }}:{{ lineRange }}
        <RepoCommitLink
          v-if="anchor.commitSha"
          :owner="owner"
          :repo="repoSlug"
          :sha="anchor.commitSha"
          :from="commitLinkFrom"
          class="ml-1"
        />
        <UBadge
          v-if="isOutdated"
          color="warning"
          variant="subtle"
          size="sm"
          class="ml-2"
        >
          {{ t('repo.discussions.anchorOutdated') }}
        </UBadge>
      </span>
      <span class="shrink-0 text-[var(--ogb-text-muted)]">{{ expanded ? '▾' : '▸' }}</span>
    </button>

    <div
      v-if="!expanded"
      class="border-t px-3 py-2 font-mono text-xs text-[var(--ogb-text-muted)]"
      style="border-color: var(--ogb-border);"
    >
      L{{ anchor.line }} …
    </div>

    <div
      v-else
      class="space-y-2 border-t p-2"
      style="border-color: var(--ogb-border);"
    >
      <div
        v-if="loading"
        class="text-xs text-[var(--ogb-text-muted)]"
      >
        {{ t('common.loading') }}
      </div>
      <RepoHighlightedCode
        v-else-if="snippet"
        :source="snippet"
        :path="anchor.filePath"
        :start-line="snippetStartLine"
      />
      <NuxtLink
        :to="fileUrl"
        class="inline-flex items-center gap-1 text-xs text-[var(--ogb-accent)] hover:underline"
      >
        {{ t('repo.discussions.viewFullFile') }} →
      </NuxtLink>
    </div>
  </div>
</template>
