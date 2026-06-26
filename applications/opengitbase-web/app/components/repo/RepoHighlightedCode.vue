<script setup lang="ts">
import type { Discussion } from '~/utils/api'
import { highlightSourceCode } from '~/utils/codeHighlight'
import DiscussionLineSelectionBar from '~/components/discussions/DiscussionLineSelectionBar.vue'

const props = withDefaults(
  defineProps<{
    source: string
    path: string
    startLine?: number
    language?: string
    linePickEnabled?: boolean
    linePickRevision?: number
    owner?: string
    repoSlug?: string
    refName?: string
    commitSha?: string
    discussions?: Discussion[]
  }>(),
  {
    startLine: 1,
    linePickEnabled: false,
    linePickRevision: 0,
    discussions: () => [],
  },
)

const emit = defineEmits<{
  rangeSelected: [payload: { line: number, endLine: number | null }]
  rangeCleared: []
}>()

const { t } = useI18n()
const colorMode = useColorMode()
const outputRef = ref<HTMLElement | null>(null)
const highlightedHtml = ref('')
const isHighlighting = ref(true)

const sourceLines = computed(() => props.source.split('\n'))
const startLineCss = computed(() => String(props.startLine))

const {
  confirmedRange,
  hoverLine,
  onLineHover,
  onLineClick,
  lineClass,
  clearSelection,
  resetPick,
} = useLineRangePick(
  (range) => {
    if (props.linePickEnabled) {
      emit('rangeSelected', range)
    }
  },
  () => {
    if (props.linePickEnabled) {
      emit('rangeCleared')
    }
  },
)

const selectionAnchor = computed(() => {
  if (!confirmedRange.value || !props.refName) {
    return null
  }
  return {
    ref: props.refName,
    commitSha: props.commitSha ?? '',
    filePath: props.path,
    line: confirmedRange.value.line,
    endLine: confirmedRange.value.endLine,
  }
})

const showDiscussionActions = computed(() =>
  props.linePickEnabled
  && !!confirmedRange.value
  && !!selectionAnchor.value
  && !!props.owner
  && !!props.repoSlug,
)

defineExpose({ clearSelection })

function compactLineHtml(html: string): string {
  return html.replace(/>\s*\n\s*</g, '><')
}

function plainFallbackHtml(): string {
  const body = sourceLines.value
    .map(line => `<span class="line"><span>${line.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;')}</span></span>`)
    .join('')
  return `<pre class="shiki"><code>${body}</code></pre>`
}

async function renderHighlight(): Promise<void> {
  if (!import.meta.client) {
    return
  }
  isHighlighting.value = true
  try {
    highlightedHtml.value = compactLineHtml(await highlightSourceCode(
      props.source,
      props.path,
      colorMode.value,
      props.language,
    ))
  }
  catch {
    highlightedHtml.value = plainFallbackHtml()
  }
  isHighlighting.value = false
  await nextTick()
  applyLineSelectionState()
}

function lineNumberFromElement(lineEl: Element): number {
  if (!outputRef.value) {
    return props.startLine
  }
  const lines = outputRef.value.querySelectorAll('.line')
  const index = Array.from(lines).indexOf(lineEl)
  return index >= 0 ? props.startLine + index : props.startLine
}

function applyLineSelectionState(): void {
  if (!outputRef.value) {
    return
  }
  const lines = outputRef.value.querySelectorAll('.line')
  lines.forEach((lineEl) => {
    const lineNumber = lineNumberFromElement(lineEl)
    lineEl.classList.toggle('repo-line--in-range', lineClass(lineNumber).length > 0)
  })
}

function onContainerClick(event: MouseEvent): void {
  if (!props.linePickEnabled) {
    return
  }
  const lineEl = (event.target as HTMLElement).closest('.line')
  if (!lineEl) {
    return
  }
  onLineClick(lineNumberFromElement(lineEl))
  applyLineSelectionState()
}

function onContainerMouseMove(event: MouseEvent): void {
  if (!props.linePickEnabled) {
    return
  }
  const lineEl = (event.target as HTMLElement).closest('.line')
  if (!lineEl) {
    onLineHover(null)
  }
  else {
    onLineHover(lineNumberFromElement(lineEl))
  }
  applyLineSelectionState()
}

function onContainerMouseLeave(): void {
  if (!props.linePickEnabled) {
    return
  }
  onLineHover(null)
  applyLineSelectionState()
}

onMounted(() => {
  void renderHighlight()
})

watch(
  () => [props.source, props.path, props.language, colorMode.value] as const,
  () => {
    void renderHighlight()
  },
)

watch(
  () => [confirmedRange.value, hoverLine.value] as const,
  () => {
    applyLineSelectionState()
  },
)

watch(
  () => props.linePickRevision,
  () => {
    resetPick()
    applyLineSelectionState()
  },
)
</script>

<template>
  <div class="space-y-2 w-full min-w-0">
    <DiscussionLineSelectionBar
      v-if="showDiscussionActions"
      :anchor="selectionAnchor!"
      :owner="owner!"
      :repo-slug="repoSlug!"
      :discussions="discussions"
      @clear="clearSelection"
    />

    <div
      v-else-if="linePickEnabled"
      class="text-xs text-[var(--ogb-text-muted)]"
    >
      {{ t('repo.discussions.pickLineHint') }}
    </div>
    <div
      ref="outputRef"
      class="repo-highlighted-code w-full min-w-0 rounded-md border border-[var(--ogb-border)] bg-[var(--ogb-bg)] text-sm leading-relaxed"
      :class="{ 'repo-highlighted-code--selectable': linePickEnabled }"
      :style="{ '--repo-line-start': startLineCss }"
      @click="onContainerClick"
      @mousemove="onContainerMouseMove"
      @mouseleave="onContainerMouseLeave"
    >
      <pre
        v-if="isHighlighting"
        class="shiki m-0 w-full min-w-0 py-4 pr-4 pl-0 font-mono text-sm leading-relaxed"
      ><code><span
        v-for="(line, index) in sourceLines"
        :key="index"
        class="line"
      ><span>{{ line }}</span></span></code></pre>
      <div
        v-else
        class="repo-highlighted-code__output w-full min-w-0 [&_code]:font-mono [&_pre]:m-0 [&_pre]:w-full [&_pre]:min-w-0 [&_pre]:py-4 [&_pre]:pr-4 [&_pre]:pl-0 [&_pre]:font-mono [&_pre]:text-sm [&_pre]:leading-relaxed"
        v-html="highlightedHtml"
      />
    </div>
  </div>
</template>

<style scoped>
.repo-highlighted-code {
  overflow: visible;
}

.repo-highlighted-code :deep(pre.shiki) {
  overflow-x: auto;
  overflow-y: visible;
}

.repo-highlighted-code :deep(pre.shiki code) {
  display: block;
  counter-reset: repo-line calc(var(--repo-line-start, 1) - 1);
}

.repo-highlighted-code :deep(.line) {
  display: block;
  position: relative;
  padding-left: 3.25rem;
  white-space: pre;
  line-height: 1.5;
}

.repo-highlighted-code :deep(.line)::before {
  counter-increment: repo-line;
  content: counter(repo-line);
  position: absolute;
  left: 0;
  width: 2.5rem;
  padding-right: 0.75rem;
  text-align: right;
  color: var(--ogb-text-muted);
  user-select: none;
}

.repo-highlighted-code--selectable :deep(.line) {
  cursor: pointer;
}

.repo-highlighted-code--selectable :deep(.line:hover) {
  background: color-mix(in srgb, var(--ogb-surface) 80%, transparent);
}

.repo-highlighted-code :deep(.line.repo-line--in-range) {
  background: rgb(20 184 166 / 0.2);
}

.repo-highlighted-code--selectable :deep(.line.repo-line--in-range:hover) {
  background: rgb(20 184 166 / 0.28);
}
</style>
