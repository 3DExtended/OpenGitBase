<script setup lang="ts">
import type { DiscussionsListPageContext } from '~/composables/useDiscussionsListPage'
import DiscussionListFilters from '~/components/discussions/DiscussionListFilters.vue'
import DiscussionListHeader from '~/components/discussions/DiscussionListHeader.vue'

defineProps<{ ctx: DiscussionsListPageContext }>()
</script>

<template>
  <div class="mx-auto max-w-2xl space-y-6 pb-24">
    <DiscussionListHeader :ctx="ctx" />

    <div
      v-if="ctx.loading"
      class="text-sm text-[var(--ogb-text-muted)]"
    >
      {{ ctx.t('common.loading') }}
    </div>
    <p
      v-else-if="ctx.notFound || ctx.forbidden"
      class="text-sm text-[var(--ogb-text-muted)]"
    >
      {{ ctx.forbidden ? ctx.t('repo.browse.forbidden') : ctx.t('repo.notFound') }}
    </p>
    <template v-else>
      <DiscussionListFilters :ctx="ctx" />
      <div
        v-if="ctx.listLoading"
        class="text-sm text-[var(--ogb-text-muted)]"
      >
        {{ ctx.t('common.loading') }}
      </div>
      <div
        v-else
        class="space-y-3"
      >
        <NuxtLink
          v-for="d in ctx.discussions"
          :key="d.id"
          :to="`/${ctx.owner}/${ctx.repoSlug}/discussions/${d.number}`"
          class="flex gap-3 rounded-2xl border p-4 transition hover:shadow-md"
          style="border-color: var(--ogb-border); background: var(--ogb-surface);"
        >
          <div
            class="w-1 shrink-0 rounded-full"
            :class="{
              'bg-emerald-500': d.status === 'Open',
              'bg-sky-500': d.status === 'Engaged',
              'bg-zinc-400': d.status === 'Resolved',
              'bg-amber-500': d.status === 'Dismissed',
            }"
          />
          <div class="min-w-0 flex-1 space-y-1.5">
            <div class="flex items-baseline justify-between gap-2">
              <p class="truncate font-medium">
                #{{ d.number }} · {{ d.title }}
              </p>
              <span class="shrink-0 text-xs text-[var(--ogb-text-muted)]"><RelativeTime :iso="d.updatedAt" /></span>
            </div>
            <p class="text-xs text-[var(--ogb-text-muted)]">
              {{ ctx.statusLabel(d.status) }}
            </p>
          </div>
        </NuxtLink>
        <p
          v-if="!ctx.discussions.length"
          class="py-8 text-center text-sm text-[var(--ogb-text-muted)]"
        >
          {{ ctx.signInRequired ? ctx.t('repo.discussions.signInToView') : ctx.t('repo.discussions.empty') }}
        </p>
      </div>
    </template>
  </div>
</template>
