<script setup lang="ts">
import type { CommentAnchorInput, Discussion, RepositoryContentBlob } from '~/utils/api'
import { fileNameFromPath, formatEntrySize, isMarkdownPath } from '~/utils/repoBrowse'
import { lfsBlobView } from '~/utils/lfsBlob'

const props = defineProps<{
  blob: RepositoryContentBlob
  rawUrl: string
  linePickEnabled?: boolean
  linePickRevision?: number
  owner?: string
  repoSlug?: string
  refName?: string
  commitSha?: string
  discussions?: Discussion[]
}>()

const emit = defineEmits<{
  lineAnchor: [anchor: CommentAnchorInput]
  lineAnchorCleared: []
}>()

const { t } = useI18n()

const fileName = computed(() => fileNameFromPath(props.blob.path))
const isMarkdown = computed(() => isMarkdownPath(props.blob.path))
const markdownMode = ref<'rendered' | 'raw'>('rendered')

// A file stored with Git LFS is shown by its object, never by the pointer text.
const lfsView = computed(() => lfsBlobView(props.blob))
const displaySize = computed(() => props.blob.lfs?.size ?? props.blob.size)
const canDownload = computed(() => lfsView.value !== 'unavailable')

const imageSrc = computed(() => {
  if (props.blob.previewKind !== 'image' || props.blob.isTooLarge) {
    return null
  }
  if (props.blob.contentBase64) {
    return `data:application/octet-stream;base64,${props.blob.contentBase64}`
  }
  return props.rawUrl
})

const showText = computed(() =>
  !lfsView.value
  && !props.blob.isBinary
  && !props.blob.isTooLarge
  && props.blob.previewKind === 'text'
  && (!isMarkdown.value || markdownMode.value === 'raw'),
)

const showMarkdown = computed(() =>
  !lfsView.value
  && !props.blob.isBinary
  && !props.blob.isTooLarge
  && isMarkdown.value
  && markdownMode.value === 'rendered'
  && props.blob.textContent,
)

const showImage = computed(() =>
  !lfsView.value
  && props.blob.previewKind === 'image'
  && !props.blob.isTooLarge
  && imageSrc.value,
)

const showSvgMessage = computed(() =>
  !lfsView.value && props.blob.previewKind === 'svg' && !props.blob.isTooLarge,
)

const showBinary = computed(() => !lfsView.value && props.blob.isBinary)
const showTooLarge = computed(() => !lfsView.value && props.blob.isTooLarge && !props.blob.isBinary)

function onRangeSelected(payload: { line: number, endLine: number | null }): void {
  emit('lineAnchor', {
    ref: props.blob.ref,
    commitSha: '',
    filePath: props.blob.path,
    line: payload.line,
    endLine: payload.endLine,
  })
}

function onRangeCleared(): void {
  emit('lineAnchorCleared')
}
</script>

<template>
  <div class="space-y-4 w-full min-w-0">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <div>
        <h2 class="font-mono text-lg font-semibold">
          {{ fileName }}
        </h2>
        <div class="mt-1 flex flex-wrap items-center gap-2 text-sm text-[var(--ogb-text-muted)]">
          <span>{{ formatEntrySize(displaySize) }}</span>
          <UBadge
            v-if="lfsView"
            color="neutral"
            variant="subtle"
            size="sm"
            icon="i-lucide-hard-drive-download"
            data-testid="lfs-badge"
          >
            {{ t('repo.browse.lfsBadge') }}
          </UBadge>
        </div>
      </div>
      <div class="flex flex-wrap items-center gap-2">
        <div
          v-if="isMarkdown && !blob.isBinary && !blob.isTooLarge"
          class="inline-flex rounded-md border border-[var(--ogb-border)] p-0.5 text-sm"
        >
          <button
            type="button"
            class="rounded px-2 py-1"
            :class="markdownMode === 'rendered'
              ? 'bg-[var(--ogb-bg)] font-medium'
              : 'text-[var(--ogb-text-muted)]'"
            @click="markdownMode = 'rendered'"
          >
            {{ t('repo.browse.rendered') }}
          </button>
          <button
            type="button"
            class="rounded px-2 py-1"
            :class="markdownMode === 'raw'
              ? 'bg-[var(--ogb-bg)] font-medium'
              : 'text-[var(--ogb-text-muted)]'"
            @click="markdownMode = 'raw'"
          >
            {{ t('repo.browse.raw') }}
          </button>
        </div>
        <UButton
          v-if="canDownload"
          :to="rawUrl"
          target="_blank"
          variant="soft"
          size="sm"
          external
        >
          {{ t('repo.browse.download') }}
        </UButton>
        <UButton
          v-else
          variant="soft"
          size="sm"
          disabled
        >
          {{ t('repo.browse.download') }}
        </UButton>
      </div>
    </div>

    <div
      v-if="lfsView === 'unavailable'"
      class="rounded-md border border-[var(--ogb-border)] bg-[var(--ogb-bg)] px-4 py-6 text-sm"
      data-testid="lfs-not-available"
    >
      <p class="font-medium">
        {{ t('repo.browse.lfsNotAvailable') }}
      </p>
      <p class="mt-1 text-[var(--ogb-text-muted)]">
        {{ t('repo.browse.lfsNotAvailableDescription') }}
      </p>
    </div>

    <div
      v-else-if="lfsView === 'image'"
      class="rounded-md border border-[var(--ogb-border)] bg-[var(--ogb-bg)] p-4"
    >
      <img
        :src="rawUrl"
        :alt="fileName"
        class="max-h-[32rem] max-w-full object-contain"
      >
    </div>

    <div
      v-else-if="lfsView === 'download'"
      class="rounded-md border border-[var(--ogb-border)] bg-[var(--ogb-bg)] px-4 py-6 text-sm text-[var(--ogb-text-muted)]"
    >
      {{ t('repo.browse.lfsDownloadOnly') }}
    </div>

    <div
      v-else-if="showTooLarge"
      class="rounded-md border border-[var(--ogb-border)] bg-[var(--ogb-bg)] px-4 py-6 text-sm text-[var(--ogb-text-muted)]"
    >
      {{ t('repo.browse.tooLarge') }}
    </div>

    <div
      v-else-if="showBinary"
      class="rounded-md border border-[var(--ogb-border)] bg-[var(--ogb-bg)] px-4 py-6 text-sm text-[var(--ogb-text-muted)]"
    >
      {{ t('repo.browse.binaryNotShown') }}
    </div>

    <div
      v-else-if="showSvgMessage"
      class="rounded-md border border-[var(--ogb-border)] bg-[var(--ogb-bg)] px-4 py-6 text-sm text-[var(--ogb-text-muted)]"
    >
      {{ t('repo.browse.svgDownloadOnly') }}
    </div>

    <div
      v-else-if="showImage"
      class="rounded-md border border-[var(--ogb-border)] bg-[var(--ogb-bg)] p-4"
    >
      <img
        :src="imageSrc!"
        :alt="fileName"
        class="max-h-[32rem] max-w-full object-contain"
      >
    </div>

    <RepoMarkdown
      v-else-if="showMarkdown"
      :source="blob.textContent!"
    />

    <RepoHighlightedCode
      v-else-if="showText"
      :source="blob.textContent!"
      :path="blob.path"
      :line-pick-enabled="linePickEnabled"
      :line-pick-revision="linePickRevision"
      :owner="owner"
      :repo-slug="repoSlug"
      :ref-name="refName"
      :commit-sha="commitSha"
      :discussions="discussions"
      @range-selected="onRangeSelected"
      @range-cleared="onRangeCleared"
    />
  </div>
</template>
