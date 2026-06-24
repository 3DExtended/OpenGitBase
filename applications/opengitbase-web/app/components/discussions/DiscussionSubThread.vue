<script setup lang="ts">
import type { CommentAnchorInput, DiscussionComment, RepositoryMember } from '~/utils/api'
import CommentAnchorPreview from '~/components/discussions/CommentAnchorPreview.vue'
import DiscussionCodeAttachModal from '~/components/discussions/DiscussionCodeAttachModal.vue'
import DiscussionRichEditor from '~/components/discussions/DiscussionRichEditor.vue'

const props = withDefaults(
  defineProps<{
    comment: DiscussionComment
    owner: string
    repoSlug: string
    memberLabel: (userId: string) => string
    members?: RepositoryMember[]
    canResolve: boolean
    canReply: boolean
    defaultRef?: string | null
  }>(),
  {
    members: () => [],
  },
)

const emit = defineEmits<{
  reply: [body: string, anchor: CommentAnchorInput | null]
  resolve: []
  unresolve: []
}>()

const { t } = useI18n()
const expanded = ref(!props.comment.isResolved)
const showReplyForm = ref(false)
const replyBody = ref('')
const replyAnchor = ref<CommentAnchorInput | null>(null)
const showAttachModal = ref(false)

watch(
  () => props.comment.isResolved,
  (isResolved) => {
    if (!isResolved) {
      expanded.value = true
    }
  },
)

function toggleExpanded(): void {
  expanded.value = !expanded.value
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
    :id="`comment-${comment.id}`"
    class="scroll-mt-24 rounded-lg border"
    :class="comment.isResolved ? 'opacity-90' : ''"
    style="border-color: var(--ogb-border);"
    data-testid="discussion-sub-thread"
  >
    <header
      class="flex flex-wrap items-center justify-between gap-2 px-3 py-2"
      style="background: var(--ogb-bg);"
    >
      <div class="flex min-w-0 flex-wrap items-center gap-2 text-xs text-[var(--ogb-text-muted)]">
        <button
          type="button"
          class="shrink-0 text-[var(--ogb-text-muted)]"
          @click="toggleExpanded"
        >
          {{ expanded ? '▾' : '▸' }}
        </button>
        <span class="font-medium text-[var(--ogb-text)]">{{ memberLabel(comment.authorUserId) }}</span>
        <span>{{ new Date(comment.createdAt).toLocaleString() }}</span>
        <UBadge
          v-if="comment.isResolved"
          color="neutral"
          variant="subtle"
          size="sm"
        >
          {{ t('repo.discussions.subThreadResolved') }}
        </UBadge>
        <UBadge
          v-if="comment.orphanedFromDeletedRoot"
          color="warning"
          variant="subtle"
          size="sm"
        >
          {{ t('repo.discussions.orphanReply') }}
        </UBadge>
        <span v-if="comment.replyCount > 0" class="text-[var(--ogb-text-muted)]">
          · {{ t('repo.discussions.replyCount', { count: comment.replyCount }) }}
        </span>
      </div>
      <div
        v-if="canResolve && !comment.orphanedFromDeletedRoot"
        class="flex gap-1"
      >
        <UButton
          v-if="!comment.isResolved"
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
      </div>
    </header>

    <div
      v-if="expanded"
      class="space-y-3 border-t px-3 py-3"
      style="border-color: var(--ogb-border);"
    >
      <CommentAnchorPreview
        v-if="comment.anchor"
        :owner="owner"
        :repo-slug="repoSlug"
        :anchor="comment.anchor"
      />
      <RepoMarkdown :source="comment.bodyMarkdown" />

      <ul
        v-if="comment.replies?.length"
        class="space-y-3 border-l pl-3"
        style="border-color: var(--ogb-border);"
      >
        <li
          v-for="reply in comment.replies"
          :id="`comment-${reply.id}`"
          :key="reply.id"
          class="space-y-2"
          data-testid="discussion-sub-thread-reply"
        >
          <div class="flex items-center justify-between gap-2 text-xs text-[var(--ogb-text-muted)]">
            <span class="font-medium text-[var(--ogb-text)]">{{ memberLabel(reply.authorUserId) }}</span>
            <span>{{ new Date(reply.createdAt).toLocaleString() }}</span>
          </div>
          <CommentAnchorPreview
            v-if="reply.anchor"
            :owner="owner"
            :repo-slug="repoSlug"
            :anchor="reply.anchor"
          />
          <RepoMarkdown :source="reply.bodyMarkdown" />
        </li>
      </ul>

      <div v-if="canReply && !comment.orphanedFromDeletedRoot">
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
          <DiscussionRichEditor
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
      {{ comment.bodyMarkdown.slice(0, 120) }}{{ comment.bodyMarkdown.length > 120 ? '…' : '' }}
    </div>

    <DiscussionCodeAttachModal
      v-model:open="showAttachModal"
      :owner="owner"
      :repo-slug="repoSlug"
      :default-ref="defaultRef"
      @select="replyAnchor = $event"
    />
  </article>
</template>
