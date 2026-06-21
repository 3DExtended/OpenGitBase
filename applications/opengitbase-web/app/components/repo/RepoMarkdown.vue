<script setup lang="ts">
import DOMPurify from 'isomorphic-dompurify'
import { marked } from 'marked'

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

const html = computed(() => {
  const parsed = marked.parse(props.source, { async: false }) as string
  return DOMPurify.sanitize(parsed, {
    USE_PROFILES: { html: true },
    FORBID_TAGS: ['script', 'style', 'iframe', 'object', 'embed'],
    FORBID_ATTR: ['onerror', 'onload', 'onclick', 'onmouseover'],
  })
})
</script>

<template>
  <div
    class="repo-markdown space-y-4 text-sm leading-relaxed [&_a]:text-[var(--ogb-accent)] [&_a]:underline [&_code]:rounded [&_code]:bg-[var(--ogb-bg)] [&_code]:px-1 [&_code]:py-0.5 [&_code]:font-mono [&_h1]:text-2xl [&_h1]:font-semibold [&_h2]:text-xl [&_h2]:font-semibold [&_h3]:text-lg [&_h3]:font-semibold [&_ol]:list-decimal [&_ol]:pl-5 [&_pre]:overflow-x-auto [&_pre]:rounded-md [&_pre]:bg-[var(--ogb-bg)] [&_pre]:p-3 [&_pre_code]:bg-transparent [&_pre_code]:p-0 [&_ul]:list-disc [&_ul]:pl-5"
    v-html="html"
  />
</template>
