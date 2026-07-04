<script setup lang="ts">
import type { CommentAnchorInput, DiscussionComment, RepositoryMember } from '~/utils/api'
import type { CollaborationThread } from '~/components/collaboration/types'

const props = withDefaults(
  defineProps<{
    comment: DiscussionComment
    owner: string
    repoSlug: string
    memberLabel: (userId: string, preferredUsername?: string | null) => string
    members?: RepositoryMember[]
    canResolve: boolean
    canReply: boolean
    defaultRef?: string | null
    commitLinkFrom?: string
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
const thread = computed<CollaborationThread>(() => {
  const mapReply = (reply: DiscussionComment) => ({
    id: reply.id,
    author: {
      userId: reply.authorUserId,
      username: reply.authorUsername,
    },
    bodyMarkdown: reply.bodyMarkdown,
    createdAt: reply.createdAt,
    anchor: reply.anchor ?? null,
  })
  return {
    id: props.comment.id,
    author: {
      userId: props.comment.authorUserId,
      username: props.comment.authorUsername,
    },
    bodyMarkdown: props.comment.bodyMarkdown,
    createdAt: props.comment.createdAt,
    isResolved: props.comment.isResolved,
    replyCount: props.comment.replyCount,
    orphanedFromDeletedRoot: props.comment.orphanedFromDeletedRoot,
    replies: props.comment.replies.map(mapReply),
    anchor: props.comment.anchor ?? null,
  }
})

function onReply(body: string, anchor: CommentAnchorInput | null): void {
  emit('reply', body, anchor)
}
</script>

<template>
  <CollaborationThread
    :thread="thread"
    :owner="owner"
    :repo-slug="repoSlug"
    :member-label="memberLabel"
    :members="members"
    :can-resolve="canResolve"
    :can-reply="canReply"
    :default-ref="defaultRef"
    :commit-link-from="commitLinkFrom"
    :resolved-label="t('repo.discussions.subThreadResolved')"
    :reply-count-label="(count: number) => t('repo.discussions.replyCount', { count })"
    @reply="onReply"
    @resolve="emit('resolve')"
    @unresolve="emit('unresolve')"
  />
</template>
