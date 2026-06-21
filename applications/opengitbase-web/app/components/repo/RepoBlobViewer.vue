<script setup lang="ts">
import type { RepositoryContentBlob } from '~/utils/api'
import { fileNameFromPath, formatEntrySize, isMarkdownPath } from '~/utils/repoBrowse'

const props = defineProps<{
  blob: RepositoryContentBlob
  rawUrl: string
}>()

const { t } = useI18n()

const fileName = computed(() => fileNameFromPath(props.blob.path))
const isMarkdown = computed(() => isMarkdownPath(props.blob.path))
const markdownMode = ref<'rendered' | 'raw'>('rendered')

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
  !props.blob.isBinary
  && !props.blob.isTooLarge
  && props.blob.previewKind === 'text'
  && (!isMarkdown.value || markdownMode.value === 'raw'),
)

const showMarkdown = computed(() =>
  !props.blob.isBinary
  && !props.blob.isTooLarge
  && isMarkdown.value
  && markdownMode.value === 'rendered'
  && props.blob.textContent,
)

const showImage = computed(() =>
  props.blob.previewKind === 'image'
  && !props.blob.isTooLarge
  && imageSrc.value,
)

const showSvgMessage = computed(() =>
  props.blob.previewKind === 'svg' && !props.blob.isTooLarge,
)

const showBinary = computed(() => props.blob.isBinary)
const showTooLarge = computed(() => props.blob.isTooLarge && !props.blob.isBinary)
</script>

<template>
  <div class="space-y-4">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <div>
        <h2 class="font-mono text-lg font-semibold">
          {{ fileName }}
        </h2>
        <p class="mt-1 text-sm text-[var(--ogb-text-muted)]">
          {{ formatEntrySize(blob.size) }}
        </p>
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
          :to="rawUrl"
          target="_blank"
          variant="soft"
          size="sm"
          external
        >
          {{ t('repo.browse.download') }}
        </UButton>
      </div>
    </div>

    <div
      v-if="showTooLarge"
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

    <pre
      v-else-if="showText"
      class="overflow-x-auto rounded-md border border-[var(--ogb-border)] bg-[var(--ogb-bg)] p-4 font-mono text-sm leading-relaxed whitespace-pre-wrap"
    ><code>{{ blob.textContent }}</code></pre>
  </div>
</template>
