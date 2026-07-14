<script setup lang="ts">
import type {
  Discussion,
  DiscussionDiscussionLink,
  DiscussionLinkType,
} from '~/utils/api'
import {
  DISCUSSION_LINK_GROUP_ORDER,
  filterLinkableDiscussions,
  groupDiscussionDiscussionLinks,
} from '~/utils/discussionDiscussionLinks'

const props = defineProps<{
  owner: string
  repoSlug: string
  discussionNumber: number
  linkedDiscussions: DiscussionDiscussionLink[]
  canManage?: boolean
  addLink: (targetDiscussionNumber: number, relationshipType: DiscussionLinkType) => Promise<boolean>
  removeLink: (link: DiscussionDiscussionLink) => Promise<void>
}>()

const canManage = computed(() => props.canManage ?? true)

const { t } = useI18n()
const api = useApi()

const expanded = ref(false)
const search = ref('')
const linkType = ref<DiscussionLinkType>('related')
const discussions = ref<Discussion[]>([])
const loadingDiscussions = ref(false)
const linking = ref(false)

const linkTypeOptions = computed(() => ([
  { label: t('repo.discussions.linkTypes.parent.label'), value: 'parent' as const },
  { label: t('repo.discussions.linkTypes.child.label'), value: 'child' as const },
  { label: t('repo.discussions.linkTypes.blocks.label'), value: 'blocks' as const },
  { label: t('repo.discussions.linkTypes.related.label'), value: 'related' as const },
]))

const linkTypeMeta = computed(() => ({
  parent: {
    label: t('repo.discussions.linkTypes.parent.label'),
    color: 'info' as const,
  },
  child: {
    label: t('repo.discussions.linkTypes.child.label'),
    color: 'info' as const,
  },
  blocks: {
    label: t('repo.discussions.linkTypes.blocks.label'),
    color: 'warning' as const,
  },
  related: {
    label: t('repo.discussions.linkTypes.related.label'),
    color: 'neutral' as const,
  },
}))

const groups = computed(() =>
  groupDiscussionDiscussionLinks(props.linkedDiscussions, DISCUSSION_LINK_GROUP_ORDER)
    .map(group => ({
      ...group,
      meta: linkTypeMeta.value[group.type],
    })),
)

const filteredDiscussions = computed(() =>
  filterLinkableDiscussions(
    discussions.value.filter(discussion => discussion.number !== props.discussionNumber),
    props.linkedDiscussions,
    search.value,
  ),
)

async function toggleExpand(): Promise<void> {
  expanded.value = !expanded.value
  if (expanded.value && !discussions.value.length) {
    loadingDiscussions.value = true
    const result = await api.discussions.list(props.owner, props.repoSlug)
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
  <div data-testid="discussion-linked-discussions">
    <UCard>
      <template #header>
        <h3 class="font-semibold">
          {{ t('repo.discussions.linkedDiscussions') }}
        </h3>
      </template>

      <div
        v-if="!linkedDiscussions.length"
        class="text-sm text-[var(--ogb-text-muted)]"
      >
        {{ t('repo.discussions.noLinkedDiscussions') }}
      </div>

      <div
        v-for="group in groups"
        :key="group.type"
        class="mb-4"
        :data-testid="`discussion-linked-group-${group.type}`"
      >
        <p class="mb-2 text-xs uppercase text-[var(--ogb-text-muted)]">
          {{ group.meta.label }}
        </p>
        <div class="space-y-2">
          <div
            v-for="link in group.links"
            :key="`${link.targetDiscussionNumber}-${link.relationshipType}`"
            class="flex items-center justify-between rounded border px-3 py-2 text-sm"
            style="border-color: var(--ogb-border);"
          >
            <NuxtLink
              :to="`/${owner}/${repoSlug}/discussions/${link.targetDiscussionNumber}`"
              class="font-medium hover:underline"
            >
              #{{ link.targetDiscussionNumber }} {{ link.targetDiscussionTitle }}
            </NuxtLink>
            <UButton
              v-if="canManage"
              size="xs"
              color="neutral"
              variant="ghost"
              :aria-label="t('repo.discussions.removeDiscussionLink')"
              @click="removeLink(link)"
            >
              {{ t('common.delete') }}
            </UButton>
          </div>
        </div>
      </div>

      <div
        v-if="canManage"
        class="border-t pt-4"
        style="border-color: var(--ogb-border);"
      >
        <UButton
          size="sm"
          variant="soft"
          data-testid="discussion-link-toggle"
          @click="toggleExpand"
        >
          {{ expanded ? t('common.cancel') : t('repo.discussions.addDiscussionLink') }}
        </UButton>

        <div
          v-if="expanded"
          class="mt-3 space-y-3"
          data-testid="discussion-link-picker"
        >
          <USelect
            v-model="linkType"
            :items="linkTypeOptions"
            size="sm"
          />
          <UInput
            v-model="search"
            size="sm"
            :placeholder="t('repo.discussions.searchDiscussions')"
          />
          <div
            v-if="loadingDiscussions"
            class="text-sm text-[var(--ogb-text-muted)]"
          >
            {{ t('common.loading') }}
          </div>
          <div
            v-else
            class="max-h-48 space-y-1 overflow-y-auto"
          >
            <UButton
              v-for="discussion in filteredDiscussions"
              :key="discussion.number"
              block
              size="sm"
              color="neutral"
              variant="ghost"
              :loading="linking"
              @click="linkDiscussion(discussion.number)"
            >
              #{{ discussion.number }} {{ discussion.title }}
            </UButton>
          </div>
        </div>
      </div>
    </UCard>
  </div>
</template>
