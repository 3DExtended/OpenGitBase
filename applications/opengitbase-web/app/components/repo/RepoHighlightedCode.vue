<script setup lang="ts">
import { highlightSourceCode } from '~/utils/codeHighlight'

const props = withDefaults(
  defineProps<{
    source: string
    path: string
    startLine?: number
  }>(),
  {
    startLine: 1,
  },
)

const colorMode = useColorMode()
const highlightedHtml = ref('')
const isHighlighting = ref(true)

const sourceLines = computed(() => props.source.split('\n'))
const startLineCss = computed(() => String(props.startLine))

async function renderHighlight(): Promise<void> {
  if (!import.meta.client) {
    return
  }
  isHighlighting.value = true
  highlightedHtml.value = await highlightSourceCode(
    props.source,
    props.path,
    colorMode.value,
  )
  isHighlighting.value = false
}

onMounted(() => {
  void renderHighlight()
})

watch(
  () => [props.source, props.path, colorMode.value] as const,
  () => {
    void renderHighlight()
  },
)
</script>

<template>
  <div
    class="repo-highlighted-code overflow-x-auto rounded-md border border-[var(--ogb-border)] bg-[var(--ogb-bg)] text-sm leading-relaxed"
    :style="{ '--repo-line-start': startLineCss }"
  >
    <pre
      v-if="isHighlighting"
      class="shiki m-0 overflow-x-auto py-4 pr-4 pl-0 font-mono text-sm leading-relaxed"
    ><code>
      <span
        v-for="(line, index) in sourceLines"
        :key="index"
        class="line"
      ><span>{{ line }}</span></span>
    </code></pre>
    <div
      v-else
      class="repo-highlighted-code__output [&_code]:font-mono [&_pre]:m-0 [&_pre]:overflow-x-auto [&_pre]:py-4 [&_pre]:pr-4 [&_pre]:pl-0 [&_pre]:font-mono [&_pre]:text-sm [&_pre]:leading-relaxed"
      v-html="highlightedHtml"
    />
  </div>
</template>

<style scoped>
.repo-highlighted-code :deep(pre.shiki code) {
  display: block;
  counter-reset: repo-line calc(var(--repo-line-start, 1) - 1);
}

.repo-highlighted-code :deep(.line) {
  display: block;
  position: relative;
  min-height: 1.5em;
  padding-left: 3.25rem;
  white-space: pre;
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
</style>
