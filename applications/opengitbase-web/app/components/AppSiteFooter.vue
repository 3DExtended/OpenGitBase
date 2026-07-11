<script setup lang="ts">
const { t } = useI18n()
</script>

<template>
  <footer
    class="border-t px-4 py-6 text-sm text-[var(--ogb-text-muted)] md:px-6"
    style="border-color: var(--ogb-border);"
  >
    <div class="mx-auto flex max-w-screen-2xl flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
      <p>{{ t('footer.tagline') }}</p>
      <div class="flex flex-wrap items-center gap-4">
        <NuxtLink
          to="/docs"
          class="text-[var(--ogb-accent)] hover:underline"
        >
          {{ t('footer.docs') }}
        </NuxtLink>
        <NuxtLink
          to="/status"
          class="text-[var(--ogb-accent)] hover:underline"
        >
          {{ t('nav.status') }}
        </NuxtLink>
      </div>
    </div>
  </footer>
</template>
