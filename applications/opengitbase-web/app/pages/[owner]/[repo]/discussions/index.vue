<script setup lang="ts">
import DiscussionCodeAttachModal from '~/components/discussions/DiscussionCodeAttachModal.vue'
import DiscussionCreateDrawer from '~/components/discussions/DiscussionCreateDrawer.vue'
import DiscussionListCards from '~/components/discussions/DiscussionListCards.vue'

const ctx = useDiscussionsListPage()
</script>

<template>
  <DiscussionListCards :ctx="ctx" />
  <DiscussionCreateDrawer
    v-model:open="ctx.showCreate"
    :title="ctx.createTitle"
    :body="ctx.createBody"
    :creating="ctx.creating"
    :error="ctx.createError"
    :tags="ctx.tags"
    :tag-ids="ctx.createTagIds"
    :members="ctx.members"
    :suggested-title="ctx.suggestedTitle"
    :anchor="ctx.pendingAnchor"
    @update:title="ctx.createTitle = $event"
    @update:body="ctx.createBody = $event"
    @update:tag-ids="ctx.createTagIds = $event"
    @submit="ctx.createDiscussion"
    @attach-code="ctx.showAttachModal = true"
    @clear-anchor="ctx.pendingAnchor = null"
  />
  <DiscussionCodeAttachModal
    v-model:open="ctx.showAttachModal"
    :owner="ctx.owner"
    :repo-slug="ctx.repoSlug"
    @select="ctx.setPendingAnchor($event)"
  />
</template>
