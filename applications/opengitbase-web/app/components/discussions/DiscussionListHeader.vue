<script setup lang="ts">
import type { DiscussionsListPageContext } from '~/composables/useDiscussionsListPage'

defineProps<{
  ctx: DiscussionsListPageContext
}>()
</script>

<template>
  <div class="space-y-6">
    <UButton
      :to="`/${ctx.owner}/${ctx.repoSlug}`"
      variant="ghost"
      icon="i-lucide-arrow-left"
      size="sm"
    >
      {{ ctx.owner }}/{{ ctx.repoSlug }}
    </UButton>

    <div class="flex flex-wrap items-center justify-between gap-4">
      <h1 class="text-2xl font-semibold">
        {{ ctx.t('repo.discussions.heading') }}
      </h1>
      <UButton
        icon="i-lucide-plus"
        @click="ctx.openCreate"
      >
        {{ ctx.t('repo.discussions.createButton') }}
      </UButton>
    </div>
  </div>
</template>
