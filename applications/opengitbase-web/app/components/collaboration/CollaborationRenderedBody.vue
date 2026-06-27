<script setup lang="ts">
import DOMPurify from 'isomorphic-dompurify'
import { marked } from 'marked'
import RepoHighlightedCode from '~/components/repo/RepoHighlightedCode.vue'
import { languageFromFenceTag } from '~/utils/codeLanguage'
import { splitMarkdownByFences } from '~/utils/markdownBlocks'

const props = defineProps<{
  source: string
}>()

marked.use({
  renderer: {
    html() {
      return ''
    },
  },
})

const segments = computed(() => splitMarkdownByFences(props.source))

const proseClass = [
  'repo-markdown__prose text-sm leading-relaxed',
  '[&_a]:text-[var(--ogb-accent)] [&_a]:underline',
  '[&_code]:rounded [&_code]:bg-[var(--ogb-bg)] [&_code]:px-1 [&_code]:py-0.5 [&_code]:font-mono',
  '[&_h1]:text-2xl [&_h1]:font-semibold',
  '[&_h2]:text-xl [&_h2]:font-semibold',
  '[&_h3]:text-lg [&_h3]:font-semibold',
  '[&_ol]:list-decimal [&_ol]:pl-5',
  '[&_ul]:list-disc [&_ul]:pl-5',
].join(' ')

function renderProse(markdown: string): string {
  const parsed = marked.parse(markdown, { async: false }) as string
  return DOMPurify.sanitize(parsed, {
    USE_PROFILES: { html: true },
    FORBID_TAGS: ['script', 'style', 'iframe', 'object', 'embed'],
    FORBID_ATTR: ['onerror', 'onload', 'onclick', 'onmouseover'],
  })
}
</script>

<template>
  <div class="repo-markdown space-y-4">
    <template
      v-for="(segment, index) in segments"
      :key="index"
    >
      <div
        v-if="segment.type === 'prose' && segment.markdown.trim()"
        :class="proseClass"
        v-html="renderProse(segment.markdown)"
      />
      <RepoHighlightedCode
        v-else
        :source="segment.source"
        path="snippet.txt"
        :language="segment.language.trim() ? languageFromFenceTag(segment.language) : undefined"
        :start-line="1"
      />
    </template>
  </div>
</template>
