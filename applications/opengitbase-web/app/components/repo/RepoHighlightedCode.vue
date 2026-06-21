<script setup lang="ts">
import { highlightSourceCode } from '~/utils/codeHighlight'

const props = defineProps<{
  source: string
  path: string
}>()

const colorMode = useColorMode()
const highlightedHtml = ref('')
const isHighlighting = ref(true)

async function renderHighlight(): Promise<void> {
  isHighlighting.value = true
  highlightedHtml.value = await highlightSourceCode(
    props.source,
    props.path,
    colorMode.value,
  )
  isHighlighting.value = false
}

watch(
  () => [props.source, props.path, colorMode.value] as const,
  () => {
    void renderHighlight()
  },
  { immediate: true },
)
</script>

<template>
  <div
    class="repo-highlighted-code overflow-x-auto rounded-md border border-[var(--ogb-border)] bg-[var(--ogb-bg)] text-sm leading-relaxed [&_code]:font-mono [&_pre]:m-0 [&_pre]:overflow-x-auto [&_pre]:p-4 [&_pre]:font-mono [&_pre]:text-sm [&_pre]:leading-relaxed"
  >
    <pre
      v-if="isHighlighting"
      class="m-0 overflow-x-auto p-4 font-mono text-sm leading-relaxed whitespace-pre-wrap"
    ><code>{{ source }}</code></pre>
    <div
      v-else
      v-html="highlightedHtml"
    />
  </div>
</template>
