<script setup lang="ts">
import type { DiscussionStatus } from '~/utils/api'
import type { DiscussionsListPageContext } from '~/composables/useDiscussionsListPage'

const props = defineProps<{
  ctx: DiscussionsListPageContext
}>()
</script>

<template>
  <div class="space-y-2">
    <div class="flex flex-wrap gap-2">
      <button
        v-for="opt in props.ctx.statusOptions"
        :key="opt.value"
        type="button"
        class="rounded-full px-3 py-1 text-xs font-medium transition"
        :class="props.ctx.statusFilter === opt.value
          ? 'bg-teal-600 text-white'
          : 'bg-[var(--ogb-bg)] text-[var(--ogb-text-muted)] hover:text-[var(--ogb-text)]'"
        @click="props.ctx.statusFilter = opt.value as DiscussionStatus | 'all'"
      >
        {{ opt.label }}
      </button>
    </div>
    <div
      v-if="props.ctx.tags.length"
      class="flex flex-wrap gap-2"
    >
      <button
        type="button"
        class="rounded-full px-3 py-1 text-xs transition"
        :class="props.ctx.tagFilter === 'all'
          ? 'bg-teal-600 text-white'
          : 'bg-[var(--ogb-bg)] text-[var(--ogb-text-muted)] hover:text-[var(--ogb-text)]'"
        @click="props.ctx.tagFilter = 'all'"
      >
        {{ props.ctx.t('repo.discussions.filters.allTags') }}
      </button>
      <button
        v-for="tag in props.ctx.tags"
        :key="tag.id"
        type="button"
        class="rounded-full px-3 py-1 text-xs transition"
        :class="props.ctx.tagFilter === tag.id
          ? 'bg-teal-600 text-white'
          : 'bg-[var(--ogb-bg)] text-[var(--ogb-text-muted)] hover:text-[var(--ogb-text)]'"
        @click="props.ctx.tagFilter = tag.id"
      >
        {{ tag.name }}
      </button>
    </div>
  </div>
</template>
