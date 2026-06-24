<script setup lang="ts">
/** PROTOTYPE — shared detail layout (grill v2): right meta sidebar + bottom composer. */
import type { CommentAnchorInput } from '~/utils/api'
import type { DiscussionDetailPageContext } from '~/composables/useDiscussionDetailPage'
import CommentAnchorPreview from '~/components/discussions/CommentAnchorPreview.vue'
import DiscussionCodeAttachModal from '~/components/discussions/DiscussionCodeAttachModal.vue'
import DiscussionRichEditor from '~/components/discussions/DiscussionRichEditor.vue'

const ctx = inject<DiscussionDetailPageContext>('discussionDetailCtx')
if (!ctx) {
  throw new Error('discussionDetailCtx is required')
}
</script>

<template>
  <div class="mx-auto max-w-6xl">
    <UButton
      :to="`/${ctx.owner}/${ctx.repoSlug}/discussions`"
      variant="ghost"
      icon="i-lucide-arrow-left"
      size="sm"
      class="mb-4"
    >
      {{ ctx.t('repo.discussions.backToList') }}
    </UButton>

    <div
      v-if="ctx.loading || ctx.pageLoading"
      class="text-sm text-[var(--ogb-text-muted)]"
    >
      {{ ctx.t('common.loading') }}
    </div>

    <UCard v-else-if="ctx.notFound || ctx.forbidden || !ctx.discussion">
      <p class="text-sm text-[var(--ogb-text-muted)]">
        {{ ctx.forbidden ? ctx.t('repo.browse.forbidden') : ctx.t('repo.discussions.notFound') }}
      </p>
    </UCard>

    <div
      v-else
      class="grid items-start gap-6 lg:grid-cols-[minmax(0,1fr)_220px]"
    >
      <main class="min-w-0">
        <div class="space-y-6">
          <header class="space-y-3">
            <h1 class="text-2xl font-semibold">
              {{ ctx.discussion.title }}
            </h1>
            <div class="flex flex-wrap gap-2">
              <UBadge
                :color="ctx.statusColor(ctx.discussion.status)"
                variant="subtle"
              >
                {{ ctx.statusLabel(ctx.discussion.status) }}
              </UBadge>
              <UBadge
                v-for="tag in ctx.discussion.tags"
                :key="tag.id"
                color="neutral"
                variant="subtle"
                size="sm"
              >
                {{ tag.name }}
              </UBadge>
            </div>
          </header>

          <UCard v-if="ctx.discussion.body">
            <RepoMarkdown :source="ctx.discussion.body" />
          </UCard>

          <section class="space-y-4">
            <h2 class="text-sm font-semibold uppercase tracking-wide text-[var(--ogb-text-muted)]">
              {{ ctx.t('repo.discussions.commentsTitle') }}
            </h2>

            <p
              v-if="!ctx.comments.length"
              class="text-sm text-[var(--ogb-text-muted)]"
            >
              {{ ctx.t('repo.discussions.noComments') }}
            </p>

            <div
              v-for="comment in ctx.comments"
              :key="comment.id"
              class="space-y-2"
            >
              <DiscussionSubThread
                :comment="comment"
                :owner="ctx.owner"
                :repo-slug="ctx.repoSlug"
                :member-label="ctx.memberLabel"
                :can-resolve="ctx.canResolveSubThread(comment)"
                :can-reply="ctx.auth.isAuthenticated"
                @reply="(body: string, anchor: CommentAnchorInput | null) => ctx.postReply(comment.id, body, anchor)"
                @resolve="ctx.resolveSubThread(comment.id)"
                @unresolve="ctx.unresolveSubThread(comment.id)"
              />
            </div>
          </section>
        </div>

        <div
          class="sticky bottom-16 z-10 mt-6 border-t bg-[var(--ogb-bg)]/95 pb-3 backdrop-blur"
          style="border-color: var(--ogb-border);"
        >
          <form
            class="space-y-2 pt-3"
            @submit.prevent="ctx.postComment"
          >
            <p
              v-if="ctx.isClosed"
              class="text-xs text-[var(--ogb-text-muted)]"
            >
              {{ ctx.t('repo.discussions.reopenHint') }}
            </p>
            <div
              v-if="ctx.commentAnchor"
              class="flex items-center justify-between rounded border px-2 py-1 text-xs font-mono"
              style="border-color: var(--ogb-border);"
            >
              <span>{{ ctx.commentAnchor.filePath }}:{{ ctx.commentAnchor.line }}</span>
              <UButton
                size="xs"
                variant="ghost"
                icon="i-lucide-x"
                @click.prevent="ctx.commentAnchor = null"
              />
            </div>
            <DiscussionRichEditor
              v-model="ctx.commentBody"
              :members="ctx.members"
              :placeholder="ctx.t('repo.discussions.commentPlaceholder')"
              min-height="5rem"
            />
            <div class="flex flex-wrap gap-2">
              <UButton
                variant="outline"
                size="sm"
                icon="i-lucide-git-branch"
                @click.prevent="ctx.showAttachModal = true"
              >
                {{ ctx.t('repo.discussions.attachToCode') }}
              </UButton>
              <UButton
                type="submit"
                :loading="ctx.posting"
                :disabled="!ctx.commentBody.trim()"
              >
                {{ ctx.t('repo.discussions.postComment') }}
              </UButton>
            </div>
            <UAlert
              v-if="ctx.postError"
              color="error"
              variant="subtle"
              :description="ctx.postError"
            />
          </form>
        </div>
      </main>

      <aside
        class="space-y-4 rounded-lg border p-4 text-sm lg:sticky lg:top-4"
        style="border-color: var(--ogb-border);"
      >
        <div>
          <p class="text-xs uppercase text-[var(--ogb-text-muted)]">Discussion</p>
          <p class="mt-1 font-mono">#{{ ctx.discussion.number }}</p>
        </div>
        <div>
          <p class="text-xs uppercase text-[var(--ogb-text-muted)]">Status</p>
          <UBadge
            :color="ctx.statusColor(ctx.discussion.status)"
            variant="subtle"
            class="mt-1"
          >
            {{ ctx.statusLabel(ctx.discussion.status) }}
          </UBadge>
        </div>
        <div v-if="ctx.discussion.assigneeUserId">
          <p class="text-xs uppercase text-[var(--ogb-text-muted)]">{{ ctx.t('repo.discussions.assignee') }}</p>
          <p class="mt-1">{{ ctx.memberLabel(ctx.discussion.assigneeUserId) }}</p>
        </div>
        <div>
          <p class="text-xs uppercase text-[var(--ogb-text-muted)]">{{ ctx.t('repo.discussions.opened') }}</p>
          <p class="mt-1 text-xs">{{ new Date(ctx.discussion.createdAt).toLocaleString() }}</p>
        </div>
        <div>
          <p class="text-xs uppercase text-[var(--ogb-text-muted)]">{{ ctx.t('repo.discussions.updated') }}</p>
          <p class="mt-1 text-xs">{{ new Date(ctx.discussion.updatedAt).toLocaleString() }}</p>
        </div>
        <div
          v-if="ctx.isWriterPlus && !ctx.isClosed"
          class="space-y-2 border-t pt-4"
          style="border-color: var(--ogb-border);"
        >
          <UButton
            block
            size="sm"
            color="success"
            variant="soft"
            :loading="ctx.resolving"
            @click="ctx.resolveDiscussion"
          >
            {{ ctx.t('repo.discussions.resolve') }}
          </UButton>
          <UButton
            block
            size="sm"
            color="warning"
            variant="soft"
            :loading="ctx.dismissing"
            @click="ctx.dismissDiscussion"
          >
            {{ ctx.t('repo.discussions.dismiss') }}
          </UButton>
        </div>
      </aside>
    </div>

    <DiscussionCodeAttachModal
      v-model:open="ctx.showAttachModal"
      :owner="ctx.owner"
      :repo-slug="ctx.repoSlug"
      @select="ctx.commentAnchor = $event"
    />
  </div>
</template>
