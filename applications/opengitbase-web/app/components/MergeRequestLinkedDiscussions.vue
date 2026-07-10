<script setup lang="ts">
import type {
  Discussion,
  MergeRequestDiscussionLink,
  MergeRequestLinkType,
} from '~/utils/api'
import {
  DISCUSSION_LINK_GROUP_ORDER,
  filterLinkableDiscussions,
  groupMergeRequestDiscussionLinks,
} from '~/utils/mergeRequestDiscussionLinks'

const props = defineProps<{
  owner: string
  repoSlug: string
  linkedDiscussions: MergeRequestDiscussionLink[]
  canManage?: boolean
  addLink: (discussionNumber: number, relationshipType: MergeRequestLinkType) => Promise<boolean>
  removeLink: (link: MergeRequestDiscussionLink) => Promise<void>
}>()

const canManage = computed(() => props.canManage ?? true)

const { t } = useI18n()
const api = useApi()

const expanded = ref(false)
const search = ref('')
const linkType = ref<MergeRequestLinkType>('related')
const discussions = ref<Discussion[]>([])
const loadingDiscussions = ref(false)
const linking = ref(false)

const linkTypeOptions = computed(() => ([
  { label: t('repo.mergeRequests.linkTypes.closes.label'), value: 'closes' as const },
  { label: t('repo.mergeRequests.linkTypes.related.label'), value: 'related' as const },
  { label: t('repo.mergeRequests.linkTypes.implements.label'), value: 'implements' as const },
]))

const linkTypeMeta = computed(() => ({
  closes: {
    label: t('repo.mergeRequests.linkTypes.closes.label'),
    hint: t('repo.mergeRequests.linkTypes.closes.hint'),
    color: 'success' as const,
  },
  related: {
    label: t('repo.mergeRequests.linkTypes.related.label'),
    hint: t('repo.mergeRequests.linkTypes.related.hint'),
    color: 'neutral' as const,
  },
  implements: {
    label: t('repo.mergeRequests.linkTypes.implements.label'),
    hint: t('repo.mergeRequests.linkTypes.implements.hint'),
    color: 'info' as const,
  },
}))

const groups = computed(() => {
  return groupMergeRequestDiscussionLinks(props.linkedDiscussions, DISCUSSION_LINK_GROUP_ORDER)
    .map(group => ({
      ...group,
      meta: linkTypeMeta.value[group.type],
    }))
})

const filteredDiscussions = computed(() => {
  return filterLinkableDiscussions(discussions.value, props.linkedDiscussions, search.value)
})

async function toggleExpand(): Promise<void> {
  expanded.value = !expanded.value
  if (expanded.value && !discussions.value.length) {
    loadingDiscussions.value = true
    const result = await api.discussions.list(props.owner, props.repoSlug, { status: 'Open' })
    discussions.value = result.data ?? []
    loadingDiscussions.value = false
  }
}

async function linkDiscussion(number: number): Promise<void> {
  linking.value = true
  const added = await props.addLink(number, linkType.value)
  linking.value = false
  if (!added) {
    return
  }
  search.value = ''
  expanded.value = false
}
</script>

<template>
  <div data-testid="mr-linked-discussions">
    <UCard>
    <template #header>
      <h3 class="font-semibold">
        {{ t('repo.mergeRequests.linkedDiscussions') }}
      </h3>
    </template>

    <div class="space-y-4">
      <p
        v-if="!linkedDiscussions.length"
        class="text-xs text-[var(--ogb-text-muted)]"
      >
        {{ t('repo.mergeRequests.noLinkedDiscussions') }}
      </p>

      <section
        v-for="group in groups"
        :key="group.type"
        class="space-y-2"
        :data-testid="`mr-linked-discussion-group-${group.type}`"
      >
        <div class="flex items-center gap-2">
          <UBadge
            variant="subtle"
            size="sm"
            :color="group.meta.color"
          >
            {{ group.meta.label }}
          </UBadge>
          <span class="text-[10px] text-[var(--ogb-text-muted)]">{{ group.meta.hint }}</span>
        </div>
        <div
          v-for="link in group.links"
          :key="`${link.discussionNumber}-${link.relationshipType}`"
          class="rounded-lg border p-3"
          style="border-color: var(--ogb-border);"
        >
          <div class="flex items-start justify-between gap-2">
            <div class="min-w-0">
              <NuxtLink
                :to="`/${owner}/${repoSlug}/discussions/${link.discussionNumber}`"
                class="block truncate text-sm font-medium hover:underline"
              >
                #{{ link.discussionNumber }} {{ link.discussionTitle }}
              </NuxtLink>
              <p
                v-if="link.discussionStatus"
                class="mt-1 text-[10px] capitalize text-[var(--ogb-text-muted)]"
              >
                {{ link.discussionStatus }}
              </p>
            </div>
            <UButton
              v-if="canManage"
              variant="ghost"
              size="xs"
              icon="i-lucide-unlink"
              :aria-label="t('repo.mergeRequests.removeDiscussionLink')"
              @click="props.removeLink(link)"
            />
          </div>
        </div>
      </section>

      <div
        v-if="canManage"
        class="rounded-lg border"
        style="border-color: var(--ogb-border);"
      >
        <button
          type="button"
          class="flex w-full items-center justify-between px-3 py-2 text-sm font-medium"
          data-testid="mr-link-discussion-expand"
          @click="toggleExpand"
        >
          <span class="flex items-center gap-2">
            <UIcon name="i-lucide-plus-circle" class="size-4" />
            {{ t('repo.mergeRequests.linkAnotherDiscussion') }}
          </span>
          <UIcon
            :name="expanded ? 'i-lucide-chevron-up' : 'i-lucide-chevron-down'"
            class="size-4 text-[var(--ogb-text-muted)]"
          />
        </button>

        <div
          v-if="expanded"
          class="space-y-3 border-t px-3 pb-3 pt-2"
          style="border-color: var(--ogb-border);"
        >
          <USelect
            v-model="linkType"
            :items="linkTypeOptions"
            value-key="value"
            label-key="label"
          />
          <UInput
            v-model="search"
            icon="i-lucide-search"
            :placeholder="t('repo.mergeRequests.filterOpenDiscussions')"
          />
          <div
            class="max-h-40 overflow-y-auto rounded border"
            style="border-color: var(--ogb-border);"
            data-testid="mr-discussion-link-picker"
          >
            <p
              v-if="loadingDiscussions"
              class="p-3 text-xs text-[var(--ogb-text-muted)]"
            >
              {{ t('common.loading') }}
            </p>
            <button
              v-for="discussion in filteredDiscussions"
              :key="discussion.number"
              type="button"
              class="flex w-full items-center gap-2 border-b px-3 py-2 text-left text-sm last:border-0 hover:bg-[var(--ogb-bg)]"
              style="border-color: var(--ogb-border);"
              :disabled="linking"
              @click="linkDiscussion(discussion.number)"
            >
              <span class="font-mono text-xs text-[var(--ogb-text-muted)]">#{{ discussion.number }}</span>
              <span class="truncate">{{ discussion.title }}</span>
            </button>
            <p
              v-if="!loadingDiscussions && !filteredDiscussions.length"
              class="p-3 text-xs text-[var(--ogb-text-muted)]"
            >
              {{ t('repo.mergeRequests.noDiscussionMatches') }}
            </p>
          </div>
        </div>
      </div>
    </div>
    </UCard>
  </div>
</template>
