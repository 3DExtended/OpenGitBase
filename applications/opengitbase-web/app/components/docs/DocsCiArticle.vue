<script setup lang="ts">
import type { CiDocPage } from '~/data/ciDocsPages'
import { CI_DOCS_SECTION_TITLE, ciDocsPages } from '~/data/ciDocsPages'

const props = defineProps<{
  page: CiDocPage
}>()

const { t } = useI18n()

const navItems = computed(() =>
  ciDocsPages.map((entry) => ({
    label: entry.title,
    to: `/docs/ci/${entry.slug}`,
    description: entry.description,
  })),
)

useHead({
  title: () => `${props.page.title} · ${CI_DOCS_SECTION_TITLE} · ${t('docs.title')}`,
})
</script>

<template>
  <div class="mx-auto flex max-w-6xl flex-col gap-8 lg:flex-row lg:items-start">
    <aside class="w-full shrink-0 lg:sticky lg:top-24 lg:w-64">
      <div class="space-y-1">
        <NuxtLink
          to="/docs"
          class="text-sm text-[var(--ogb-text-muted)] hover:text-[var(--ogb-accent)]"
        >
          {{ t('docs.backToIndex') }}
        </NuxtLink>
        <p class="mt-3 text-xs font-semibold uppercase tracking-wide text-[var(--ogb-text-muted)]">
          {{ CI_DOCS_SECTION_TITLE }}
        </p>
      </div>
      <nav class="mt-3 space-y-1">
        <NuxtLink
          v-for="item in navItems"
          :key="item.to"
          :to="item.to"
          class="block rounded-md px-3 py-2 text-sm transition-colors"
          :class="item.to === `/docs/ci/${page.slug}`
            ? 'bg-[var(--ogb-accent-soft)] font-medium text-[var(--ogb-accent)]'
            : 'text-[var(--ogb-text-muted)] hover:bg-[var(--ogb-bg)] hover:text-[var(--ogb-text)]'"
        >
          {{ item.label }}
        </NuxtLink>
      </nav>
    </aside>

    <article class="min-w-0 flex-1">
      <header class="mb-6 border-b pb-6" style="border-color: var(--ogb-border);">
        <p class="text-sm text-[var(--ogb-text-muted)]">
          {{ t('docs.ci.sectionLabel') }}
        </p>
        <h1 class="mt-1 text-3xl font-semibold tracking-tight">
          {{ page.title }}
        </h1>
        <p class="mt-2 text-sm text-[var(--ogb-text-muted)]">
          {{ page.description }}
        </p>
      </header>

      <RepoMarkdown :source="page.markdown" />
    </article>
  </div>
</template>
