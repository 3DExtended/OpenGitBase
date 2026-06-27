<script setup lang="ts">
import type { CommentAnchorInput } from '~/utils/api'
import type { CollaborationThreadProps } from '~/components/collaboration/types'
import CommentAnchorPreview from '~/components/discussions/CommentAnchorPreview.vue'
import CollaborationCodeAttachModal from '~/components/collaboration/CollaborationCodeAttachModal.vue'
import CollaborationMarkdownEditor from '~/components/collaboration/CollaborationMarkdownEditor.vue'
import CollaborationRenderedBody from '~/components/collaboration/CollaborationRenderedBody.vue'
import { resolveTargetCommentId, subtreeContainsComment } from '~/utils/discussionCommentHash'

const props = withDefaults(defineProps<CollaborationThreadProps>(), {
  members: () => [],
  resolvedLabel: 'Resolved',
  outdatedLabel: 'Outdated',
  replyCountLabel: (count: number) => `${count} replies`,
})

const emit = defineEmits<{
  reply: [body: string, anchor: CommentAnchorInput | null]
  resolve: []
  unresolve: []
}>()

const { t } = useI18n()
const route = useRoute()
const expanded = ref(!props.thread.isResolved && !props.thread.isOutdated)
const showReplyForm = ref(false)
const replyBody = ref('')
const replyAnchor = ref<CommentAnchorInput | null>(null)
const showAttachModal = ref(false)

watch(
  () => resolveTargetCommentId({ hash: route.hash, query: route.query }),
  (commentId) => {
    if (commentId && subtreeContainsComment(props.thread as never, commentId)) {
      expanded.value = true
    }
  },
  { immediate: true },
)

watch(
  () => props.thread.isResolved,
  (isResolved) => {
    if (!isResolved) {
      expanded.value = true
    }
  },
)

function toggleExpanded(): void {
  expanded.value = !expanded.value
}

function previewText(body: string): string {
  const text = body.replace(/\s+/g, ' ').trim()
  return text.slice(0, 120)
}

async function submitReply(): Promise<void> {
  const body = replyBody.value.trim()
  if (!body) {
    return
  }
  emit('reply', body, replyAnchor.value)
  replyBody.value = ''
  replyAnchor.value = null
  showReplyForm.value = false
  expanded.value = true
}
</script>

<template>
  <article
    :id="`comment-${thread.id}`"
    class="scroll-mt-24 rounded-lg border"
    :class="thread.isResolved ? 'opacity-90' : ''"
    style="border-color: var(--ogb-border);"
    data-testid="discussion-sub-thread"
  >
    <header
      class="flex flex-wrap items-center justify-between gap-x-3 gap-y-2 px-3 py-2"
      style="background: var(--ogb-bg);"
    >
      <div class="flex min-w-0 flex-wrap items-center gap-2 text-xs text-[var(--ogb-text-muted)]">
        <button
          type="button"
          class="inline-flex shrink-0 items-center justify-center text-[var(--ogb-text-muted)] hover:text-[var(--ogb-text)]"
          :aria-expanded="expanded"
          @click="toggleExpanded"
        >
          <UIcon
            :name="expanded ? 'i-lucide-chevron-down' : 'i-lucide-chevron-right'"
            class="size-4"
          />
        </button>
        <span class="font-medium text-[var(--ogb-text)]">{{ memberLabel(thread.author.userId, thread.author.username) }}</span>
        <span v-if="thread.replyCount > 0" class="text-[var(--ogb-text-muted)]">
          {{ replyCountLabel(thread.replyCount) }}
        </span>
        <UBadge
          v-if="thread.isResolved"
          color="neutral"
          variant="subtle"
          size="sm"
        >
          {{ resolvedLabel }}
        </UBadge>
        <UBadge
          v-if="thread.isOutdated"
          color="warning"
          variant="subtle"
          size="sm"
        >
          {{ outdatedLabel }}
        </UBadge>
        <UBadge
          v-if="thread.orphanedFromDeletedRoot"
          color="warning"
          variant="subtle"
          size="sm"
        >
          {{ t('repo.discussions.orphanReply') }}
        </UBadge>
      </div>
      <div class="flex shrink-0 items-center gap-2 text-xs text-[var(--ogb-text-muted)]">
        <RelativeTime :iso="thread.createdAt" />
        <template v-if="canResolve && !thread.orphanedFromDeletedRoot">
          <UButton
            v-if="!thread.isResolved"
            size="xs"
            variant="soft"
            color="success"
            @click="emit('resolve')"
          >
            {{ t('repo.discussions.resolveSubThread') }}
          </UButton>
          <UButton
            v-else
            size="xs"
            variant="soft"
            color="neutral"
            @click="emit('unresolve')"
          >
            {{ t('repo.discussions.unresolveSubThread') }}
          </UButton>
        </template>
      </div>
    </header>

    <div
      v-if="expanded"
      class="space-y-3 border-t px-3 py-3"
      style="border-color: var(--ogb-border);"
    >
      <CommentAnchorPreview
        v-if="thread.anchor"
        :owner="owner"
        :repo-slug="repoSlug"
        :anchor="thread.anchor"
      />
      <CollaborationRenderedBody :source="thread.bodyMarkdown" />

      <ul
        v-if="thread.replies?.length"
        class="list-none space-y-3 border-l pl-3"
        style="border-color: var(--ogb-border);"
      >
        <li
          v-for="reply in thread.replies"
          :id="`comment-${reply.id}`"
          :key="reply.id"
          class="space-y-2"
          data-testid="discussion-sub-thread-reply"
        >
          <div class="flex items-center justify-between gap-2 text-xs text-[var(--ogb-text-muted)]">
            <span class="font-medium text-[var(--ogb-text)]">{{ memberLabel(reply.author.userId, reply.author.username) }}</span>
            <span><RelativeTime :iso="reply.createdAt" /></span>
          </div>
          <CommentAnchorPreview
            v-if="reply.anchor"
            :owner="owner"
            :repo-slug="repoSlug"
            :anchor="reply.anchor"
          />
          <CollaborationRenderedBody :source="reply.bodyMarkdown" />
        </li>
      </ul>

      <div v-if="canReply && !thread.orphanedFromDeletedRoot">
        <UButton
          v-if="!showReplyForm"
          size="xs"
          variant="ghost"
          icon="i-lucide-reply"
          @click="showReplyForm = true"
        >
          {{ t('repo.discussions.reply') }}
        </UButton>
        <form
          v-else
          class="space-y-2"
          @submit.prevent="submitReply"
        >
          <CollaborationMarkdownEditor
            v-model="replyBody"
            :members="members"
            :placeholder="t('repo.discussions.replyPlaceholder')"
            min-height="4rem"
          />
          <div
            v-if="replyAnchor"
            class="flex items-center justify-between rounded border px-2 py-1 text-xs font-mono"
            style="border-color: var(--ogb-border);"
          >
            <span>{{ replyAnchor.filePath }}:{{ replyAnchor.line }}</span>
            <button
              type="button"
              class="text-[var(--ogb-text-muted)] hover:text-[var(--ogb-text)]"
              @click="replyAnchor = null"
            >
              ×
            </button>
          </div>
          <div class="flex flex-wrap gap-2">
            <UButton
              type="button"
              size="xs"
              variant="soft"
              @click="showAttachModal = true"
            >
              {{ t('repo.discussions.attachToCode') }}
            </UButton>
            <UButton
              type="submit"
              size="xs"
              :disabled="!replyBody.trim()"
            >
              {{ t('repo.discussions.postReply') }}
            </UButton>
            <UButton
              type="button"
              size="xs"
              variant="ghost"
              @click="showReplyForm = false"
            >
              {{ t('common.cancel') }}
            </UButton>
          </div>
        </form>
      </div>
    </div>

    <div
      v-else
      class="border-t px-3 py-2 text-xs text-[var(--ogb-text-muted)]"
      style="border-color: var(--ogb-border);"
    >
      {{ previewText(thread.bodyMarkdown) }}{{ thread.bodyMarkdown.length > 120 ? '…' : '' }}
    </div>

    <CollaborationCodeAttachModal
      v-model:open="showAttachModal"
      :owner="owner"
      :repo-slug="repoSlug"
      :default-ref="defaultRef"
      @select="replyAnchor = $event"
    />
  </article>
</template>
