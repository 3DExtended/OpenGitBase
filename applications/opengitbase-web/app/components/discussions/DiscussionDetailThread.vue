<script setup lang="ts">
import type { CommentAnchorInput, DiscussionComment } from '~/utils/api'
import type { DiscussionDetailPageContext } from '~/composables/useDiscussionDetailPage'
import DiscussionSubThread from '~/components/discussions/DiscussionSubThread.vue'

/** A comment thread on the discussion detail page, wired to the page's permissions and actions. */
defineProps<{
  ctx: DiscussionDetailPageContext
  comment: DiscussionComment
}>()
</script>

<template>
  <DiscussionSubThread
    :comment="comment"
    :owner="ctx.owner"
    :repo-slug="ctx.repoSlug"
    :member-label="ctx.memberLabel"
    :members="ctx.members"
    :can-resolve="ctx.canResolveSubThread(comment)"
    :can-reply="ctx.auth.isAuthenticated"
    :commit-link-from="`discussions/${ctx.discussion?.number}`"
    @reply="(body: string, anchor: CommentAnchorInput | null) => ctx.postReply(comment.id, body, anchor)"
    @resolve="ctx.resolveSubThread(comment.id)"
    @unresolve="ctx.unresolveSubThread(comment.id)"
  />
</template>
