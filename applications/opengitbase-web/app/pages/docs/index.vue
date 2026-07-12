<script setup lang="ts">
import { CI_DOCS_SECTION_TITLE, ciDocsPages } from '~/data/ciDocsPages'
import { STORAGE_DOCS_SECTION_TITLE, storageDocsPages } from '~/data/storageDocsPages'

const { t } = useI18n()

useHead({ title: () => t('docs.title') })
</script>

<template>
  <div class="mx-auto max-w-4xl space-y-8">
    <header class="space-y-2">
      <h1 class="text-3xl font-semibold tracking-tight">
        {{ t('docs.title') }}
      </h1>
      <p class="text-sm text-[var(--ogb-text-muted)]">
        {{ t('docs.subtitle') }}
      </p>
    </header>

    <section class="space-y-4">
      <div>
        <h2 class="text-xl font-semibold">
          {{ STORAGE_DOCS_SECTION_TITLE }}
        </h2>
        <p class="mt-1 text-sm text-[var(--ogb-text-muted)]">
          {{ t('docs.storage.intro') }}
        </p>
      </div>

      <div class="grid gap-4 sm:grid-cols-2">
        <NuxtLink
          v-for="page in storageDocsPages"
          :key="page.slug"
          :to="`/docs/storage/${page.slug}`"
          class="rounded-lg border p-4 transition-colors hover:border-[var(--ogb-accent)]"
          style="border-color: var(--ogb-border);"
        >
          <h3 class="font-medium text-[var(--ogb-accent)]">
            {{ page.title }}
          </h3>
          <p class="mt-1 text-sm text-[var(--ogb-text-muted)]">
            {{ page.description }}
          </p>
        </NuxtLink>
      </div>
    </section>

    <section class="space-y-4">
      <div>
        <h2 class="text-xl font-semibold">
          {{ CI_DOCS_SECTION_TITLE }}
        </h2>
        <p class="mt-1 text-sm text-[var(--ogb-text-muted)]">
          {{ t('docs.ci.intro') }}
        </p>
      </div>

      <div class="grid gap-4 sm:grid-cols-2">
        <NuxtLink
          v-for="page in ciDocsPages"
          :key="page.slug"
          :to="`/docs/ci/${page.slug}`"
          class="rounded-lg border p-4 transition-colors hover:border-[var(--ogb-accent)]"
          style="border-color: var(--ogb-border);"
        >
          <h3 class="font-medium text-[var(--ogb-accent)]">
            {{ page.title }}
          </h3>
          <p class="mt-1 text-sm text-[var(--ogb-text-muted)]">
            {{ page.description }}
          </p>
        </NuxtLink>
      </div>
    </section>
  </div>
</template>
