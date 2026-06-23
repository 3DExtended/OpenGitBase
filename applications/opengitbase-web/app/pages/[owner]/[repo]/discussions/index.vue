<script setup lang="ts">
import DiscussionsListVariantA from '~/components/prototype/discussions/DiscussionsListVariantA.vue'
import DiscussionCodeAttachModal from '~/components/discussions/DiscussionCodeAttachModal.vue'
import DiscussionCreateDrawer from '~/components/discussions/DiscussionCreateDrawer.vue'
import DiscussionListFilters from '~/components/discussions/DiscussionListFilters.vue'
import DiscussionListHeader from '~/components/discussions/DiscussionListHeader.vue'

const ctx = useDiscussionsListPage()
const prototypeEnabled = useDiscussionPrototypeEnabled()
</script>

<template>
  <template v-if="prototypeEnabled">
    <DiscussionsListVariantA :ctx="ctx" />
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

  <div
    v-else
    class="mx-auto max-w-3xl space-y-6"
  >
    <DiscussionListHeader :ctx="ctx" />

    <div
      v-if="ctx.loading"
      class="text-sm text-[var(--ogb-text-muted)]"
    >
      {{ ctx.t('common.loading') }}
    </div>

    <UCard v-else-if="ctx.notFound">
      <p class="text-sm text-[var(--ogb-text-muted)]">
        {{ ctx.t('repo.notFound') }}
      </p>
    </UCard>

    <UCard v-else-if="ctx.forbidden">
      <p class="text-sm text-[var(--ogb-text-muted)]">
        {{ ctx.t('repo.browse.forbidden') }}
      </p>
    </UCard>

    <template v-else>
      <DiscussionListFilters :ctx="ctx" />

      <div
        v-if="ctx.listLoading"
        class="text-sm text-[var(--ogb-text-muted)]"
      >
        {{ ctx.t('common.loading') }}
      </div>

      <UCard v-else-if="!ctx.discussions.length">
        <p class="text-sm text-[var(--ogb-text-muted)]">
          {{ ctx.signInRequired ? ctx.t('repo.discussions.signInToView') : ctx.t('repo.discussions.empty') }}
        </p>
      </UCard>

      <ul
        v-else
        class="divide-y rounded-lg border"
        style="border-color: var(--ogb-border);"
      >
        <li
          v-for="discussion in ctx.discussions"
          :key="discussion.id"
        >
          <NuxtLink
            :to="`/${ctx.owner}/${ctx.repoSlug}/discussions/${discussion.number}`"
            class="flex flex-col gap-2 px-4 py-3 transition-colors hover:bg-[var(--ogb-bg)] sm:flex-row sm:items-center sm:justify-between"
          >
            <div class="min-w-0 space-y-1">
              <p class="truncate font-medium">
                <span class="text-[var(--ogb-text-muted)]">#{{ discussion.number }}</span>
                {{ discussion.title }}
              </p>
              <div class="flex flex-wrap items-center gap-2">
                <UBadge
                  :color="ctx.statusColor(discussion.status)"
                  variant="subtle"
                  size="sm"
                >
                  {{ ctx.statusLabel(discussion.status) }}
                </UBadge>
                <UBadge
                  v-for="tag in discussion.tags"
                  :key="tag.id"
                  color="neutral"
                  variant="subtle"
                  size="sm"
                >
                  {{ tag.name }}
                </UBadge>
              </div>
            </div>
            <p class="shrink-0 text-xs text-[var(--ogb-text-muted)]">
              {{ new Date(discussion.updatedAt).toLocaleString() }}
            </p>
          </NuxtLink>
        </li>
      </ul>
    </template>

    <UModal v-model:open="ctx.showCreate">
      <template #content>
        <UCard>
          <template #header>
            <h2 class="font-semibold">
              {{ ctx.t('repo.discussions.createTitle') }}
            </h2>
          </template>
          <form
            class="space-y-4"
            @submit.prevent="ctx.createDiscussion"
          >
            <UFormField
              :label="ctx.t('repo.discussions.fields.title')"
              required
            >
              <UInput
                v-model="ctx.createTitle"
                required
              />
            </UFormField>
            <UFormField :label="ctx.t('repo.discussions.fields.body')">
              <UTextarea
                v-model="ctx.createBody"
                :rows="4"
              />
            </UFormField>
            <UFormField
              v-if="ctx.tags.length"
              :label="ctx.t('repo.discussions.fields.tags')"
            >
              <USelectMenu
                v-model="ctx.createTagIds"
                :items="ctx.tags"
                value-key="id"
                label-key="name"
                multiple
              />
            </UFormField>
            <UAlert
              v-if="ctx.createError"
              color="error"
              variant="subtle"
              :description="ctx.createError"
            />
            <div class="flex justify-end gap-2">
              <UButton
                variant="ghost"
                @click="ctx.showCreate = false"
              >
                {{ ctx.t('common.cancel') }}
              </UButton>
              <UButton
                type="submit"
                :loading="ctx.creating"
              >
                {{ ctx.t('repo.discussions.createButton') }}
              </UButton>
            </div>
          </form>
        </UCard>
      </template>
    </UModal>
  </div>
</template>
