<script setup lang="ts">
import type { CommentAnchorInput, Discussion } from '~/utils/api'
import { anchorToQuery } from '~/utils/discussionAnchorQuery'

const props = defineProps<{
  open: boolean
  anchor: CommentAnchorInput
  owner: string
  repoSlug: string
  discussions: Discussion[]
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
}>()

const { t } = useI18n()
const showPicker = ref(false)

const openDiscussions = computed(() =>
  props.discussions.filter(d => d.status === 'Open' || d.status === 'Engaged'),
)

function baseQuery(): Record<string, string> {
  return anchorToQuery(props.anchor)
}

async function startNewDiscussion(): Promise<void> {
  await navigateTo({
    path: `/${props.owner}/${props.repoSlug}/discussions`,
    query: { ...baseQuery(), openCreate: '1' },
  })
  emit('update:open', false)
}

async function addToDiscussion(number: number): Promise<void> {
  await navigateTo({
    path: `/${props.owner}/${props.repoSlug}/discussions/${number}`,
    query: baseQuery(),
  })
  emit('update:open', false)
}
</script>

<template>
  <div
    v-if="open"
    class="fixed inset-0 z-[120] flex items-end justify-center p-4 sm:items-center"
    @click.self="emit('update:open', false)"
  >
    <div
      class="w-full max-w-sm rounded-xl border bg-[var(--ogb-surface)] p-4 shadow-xl"
      style="border-color: var(--ogb-border);"
    >
      <p class="mb-1 font-mono text-xs text-[var(--ogb-text-muted)]">
        {{ anchor.filePath }}:{{ anchor.line }}<span v-if="anchor.endLine && anchor.endLine !== anchor.line">–{{ anchor.endLine }}</span>
      </p>
      <p class="mb-4 text-sm font-medium">
        {{ t('repo.discussions.linePickComplete') }}
      </p>

      <div
        v-if="!showPicker"
        class="space-y-2"
      >
        <UButton
          block
          icon="i-lucide-plus"
          @click="startNewDiscussion"
        >
          {{ t('repo.discussions.newDiscussionFromLine') }}
        </UButton>
        <UButton
          block
          variant="outline"
          icon="i-lucide-message-square-plus"
          @click="showPicker = true"
        >
          {{ t('repo.discussions.addToExistingDiscussion') }}
        </UButton>
      </div>

      <div
        v-else
        class="space-y-2"
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
        <UButton
          variant="ghost"
          size="sm"
          @click="showPicker = false"
        >
          {{ t('common.cancel') }}
        </UButton>
      </div>
    </div>
  </div>
</template>
