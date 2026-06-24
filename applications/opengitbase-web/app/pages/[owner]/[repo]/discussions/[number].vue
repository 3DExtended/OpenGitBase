<script setup lang="ts">
import type { CommentAnchorInput } from '~/utils/api'
import DiscussionDetailHybrid from '~/components/prototype/discussions/DiscussionDetailHybrid.vue'

const ctx = useDiscussionDetailPage()
const prototypeEnabled = useDiscussionPrototypeEnabled()

provide('discussionDetailCtx', ctx)
</script>

<template>
  <template v-if="prototypeEnabled">
    <DiscussionDetailHybrid />
  </template>

  <div
    v-else
    class="mx-auto max-w-3xl space-y-6"
  >
    <UButton
      :to="`/${ctx.owner}/${ctx.repoSlug}/discussions`"
      variant="ghost"
      icon="i-lucide-arrow-left"
      size="sm"
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

    <template v-else>
      <div class="space-y-3">
        <div class="flex flex-wrap items-start justify-between gap-4">
          <div class="min-w-0 space-y-2">
            <p class="font-mono text-sm text-[var(--ogb-text-muted)]">
              #{{ ctx.discussion.number }}
            </p>
            <h1 class="text-2xl font-semibold">
              {{ ctx.discussion.title }}
            </h1>
            <div class="flex flex-wrap items-center gap-2">
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
          </div>
          <div
            v-if="ctx.isWriterPlus && !ctx.isClosed"
            class="flex flex-wrap gap-2"
          >
            <UButton
              color="success"
              variant="soft"
              size="sm"
              icon="i-lucide-check-circle"
              :loading="ctx.resolving"
              @click="ctx.resolveDiscussion"
            >
              {{ ctx.t('repo.discussions.resolve') }}
            </UButton>
            <UButton
              color="warning"
              variant="soft"
              size="sm"
              icon="i-lucide-x-circle"
              :loading="ctx.dismissing"
              @click="ctx.dismissDiscussion"
            >
              {{ ctx.t('repo.discussions.dismiss') }}
            </UButton>
          </div>
        </div>

        <p
          v-if="ctx.discussion.assigneeUserId"
          class="text-sm text-[var(--ogb-text-muted)]"
        >
          {{ ctx.t('repo.discussions.assignee') }}: {{ ctx.memberLabel(ctx.discussion.assigneeUserId) }}
        </p>

        <p class="text-xs text-[var(--ogb-text-muted)]">
          {{ ctx.t('repo.discussions.opened') }}
          {{ new Date(ctx.discussion.createdAt).toLocaleString() }}
          ·
          {{ ctx.t('repo.discussions.updated') }}
          {{ new Date(ctx.discussion.updatedAt).toLocaleString() }}
        </p>

        <UCard v-if="ctx.discussion.body">
          <RepoMarkdown :source="ctx.discussion.body" />
        </UCard>
      </div>

      <UCard>
        <template #header>
          <h2 class="font-semibold">
            {{ ctx.t('repo.discussions.commentsTitle') }}
          </h2>
        </template>

        <p
          v-if="!ctx.comments.length"
          class="text-sm text-[var(--ogb-text-muted)]"
        >
          {{ ctx.t('repo.discussions.noComments') }}
        </p>

        <ul
          v-else
          class="mb-6 space-y-4"
        >
          <li
            v-for="comment in ctx.comments"
            :key="comment.id"
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
          </li>
        </ul>

        <form
          class="space-y-3 border-t pt-4"
          style="border-color: var(--ogb-border);"
          @submit.prevent="ctx.postComment"
        >
          <p
            v-if="ctx.isClosed"
            class="text-sm text-[var(--ogb-text-muted)]"
          >
            {{ ctx.t('repo.discussions.reopenHint') }}
          </p>
          <UFormField :label="ctx.t('repo.discussions.fields.comment')">
            <UTextarea
              v-model="ctx.commentBody"
              :rows="4"
              :placeholder="ctx.t('repo.discussions.commentPlaceholder')"
            />
          </UFormField>
          <UAlert
            v-if="ctx.postError"
            color="error"
            variant="subtle"
            :description="ctx.postError"
          />
          <UButton
            type="submit"
            :loading="ctx.posting"
            :disabled="!ctx.commentBody.trim()"
          >
            {{ ctx.auth.isAuthenticated ? ctx.t('repo.discussions.postComment') : ctx.t('nav.signIn') }}
          </UButton>
        </form>
      </UCard>
    </template>
  </div>
</template>
