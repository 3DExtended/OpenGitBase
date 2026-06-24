<script setup lang="ts">
import type { CommentAnchorInput, Discussion } from '~/utils/api'
import { anchorToQuery } from '~/utils/discussionAnchorQuery'

const props = defineProps<{
  anchor: CommentAnchorInput
  owner: string
  repoSlug: string
  discussions: Discussion[]
}>()

const emit = defineEmits<{
  clear: []
}>()

const { t } = useI18n()
const showExistingPicker = ref(false)

const openDiscussions = computed(() =>
  props.discussions.filter(d => d.status === 'Open' || d.status === 'Engaged'),
)

const rangeLabel = computed(() =>
  props.anchor.endLine && props.anchor.endLine !== props.anchor.line
    ? `${props.anchor.line}–${props.anchor.endLine}`
    : `${props.anchor.line}`,
)

function baseQuery(): Record<string, string> {
  return anchorToQuery(props.anchor)
}

async function startNewDiscussion(): Promise<void> {
  await navigateTo({
    path: `/${props.owner}/${props.repoSlug}/discussions`,
    query: { ...baseQuery(), openCreate: '1' },
  })
}

async function addToDiscussion(number: number): Promise<void> {
  await navigateTo({
    path: `/${props.owner}/${props.repoSlug}/discussions/${number}`,
    query: baseQuery(),
  })
}
</script>

<template>
  <div
    class="space-y-3 rounded-lg border-2 px-4 py-3 shadow-sm"
    style="border-color: var(--ogb-accent); background: var(--ogb-surface);"
  >
    <div class="flex flex-wrap items-start justify-between gap-3">
      <div class="min-w-0 space-y-1">
        <p class="text-sm font-medium">
          {{ t('repo.discussions.lineSelectionReady') }}
        </p>
        <p class="truncate font-mono text-xs text-[var(--ogb-text-muted)]">
          {{ anchor.filePath }}:{{ rangeLabel }}
        </p>
        <p
          v-if="!anchor.endLine || anchor.endLine === anchor.line"
          class="text-xs text-[var(--ogb-text-muted)]"
        >
          {{ t('repo.discussions.pickExtendHint') }}
        </p>
      </div>
      <div class="flex flex-wrap gap-2">
        <UButton
          icon="i-lucide-plus"
          @click="startNewDiscussion"
        >
          {{ t('repo.discussions.newDiscussionFromLine') }}
        </UButton>
        <UButton
          variant="outline"
          icon="i-lucide-message-square-plus"
          @click="showExistingPicker = !showExistingPicker"
        >
          {{ t('repo.discussions.addToExistingDiscussion') }}
        </UButton>
        <UButton
          variant="ghost"
          @click="emit('clear')"
        >
          {{ t('repo.discussions.clearLineSelection') }}
        </UButton>
      </div>
    </div>

    <div
      v-if="showExistingPicker"
      class="space-y-2 border-t pt-3"
      style="border-color: var(--ogb-border);"
    >
      <p class="text-xs text-[var(--ogb-text-muted)]">
        {{ t('repo.discussions.pickDiscussion') }}
      </p>
      <button
        v-for="discussion in openDiscussions"
        :key="discussion.id"
        type="button"
        class="flex w-full items-center gap-2 rounded-md border px-3 py-2 text-left text-sm hover:bg-[var(--ogb-bg)]"
        style="border-color: var(--ogb-border);"
        @click="addToDiscussion(discussion.number)"
      >
        <span class="font-mono text-xs text-[var(--ogb-text-muted)]">#{{ discussion.number }}</span>
        <span class="min-w-0 truncate">{{ discussion.title }}</span>
      </button>
      <p
        v-if="!openDiscussions.length"
        class="text-sm text-[var(--ogb-text-muted)]"
      >
        {{ t('repo.discussions.empty') }}
      </p>
    </div>
  </div>
</template>
