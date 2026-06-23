<script setup lang="ts">
import type { CommentAnchorInput, RepositoryMember } from '~/utils/api'
import DiscussionRichEditor from '~/components/discussions/DiscussionRichEditor.vue'

const props = defineProps<{
  open: boolean
  title: string
  body: string
  creating: boolean
  error: string | null
  tags: Array<{ id: string, name: string }>
  tagIds: string[]
  members: RepositoryMember[]
  suggestedTitle?: string
  anchor?: CommentAnchorInput | null
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  'update:title': [value: string]
  'update:body': [value: string]
  'update:tagIds': [value: string[]]
  submit: []
  attachCode: []
  clearAnchor: []
}>()

const { t } = useI18n()

watch(
  () => props.open,
  (isOpen) => {
    if (isOpen && props.suggestedTitle && !props.title) {
      emit('update:title', props.suggestedTitle)
    }
  },
)
</script>

<template>
  <Teleport to="body">
    <div
      v-if="open"
      class="fixed inset-0 z-[150] flex justify-end bg-black/30"
      @click.self="emit('update:open', false)"
    >
      <aside
        class="flex h-full w-full max-w-lg flex-col border-l bg-[var(--ogb-surface)] shadow-xl"
        style="border-color: var(--ogb-border);"
      >
        <header
          class="flex items-center justify-between border-b px-5 py-4"
          style="border-color: var(--ogb-border);"
        >
          <h2 class="font-semibold">
            {{ t('repo.discussions.createTitle') }}
          </h2>
          <UButton
            variant="ghost"
            icon="i-lucide-x"
            size="sm"
            @click="emit('update:open', false)"
          />
        </header>

        <form
          class="flex flex-1 flex-col gap-4 overflow-y-auto p-5"
          @submit.prevent="emit('submit')"
        >
          <div
            v-if="anchor"
            class="flex items-center justify-between gap-2 rounded-md border px-3 py-2 text-xs"
            style="border-color: var(--ogb-border);"
          >
            <span class="font-mono text-[var(--ogb-text-muted)]">
              {{ anchor.filePath }}:{{ anchor.line }}<span v-if="anchor.endLine && anchor.endLine !== anchor.line">–{{ anchor.endLine }}</span>
            </span>
            <UButton
              size="xs"
              variant="ghost"
              icon="i-lucide-x"
              @click.prevent="emit('clearAnchor')"
            />
          </div>

          <UFormField
            :label="t('repo.discussions.fields.title')"
            required
          >
            <UInput
              :model-value="title"
              required
              autofocus
              @update:model-value="emit('update:title', $event)"
            />
          </UFormField>

          <UFormField :label="t('repo.discussions.fields.body')">
            <DiscussionRichEditor
              :model-value="body"
              :members="members"
              :placeholder="t('repo.discussions.fields.body')"
              min-height="12rem"
              @update:model-value="emit('update:body', $event)"
            />
          </UFormField>

          <UButton
            variant="outline"
            size="sm"
            icon="i-lucide-git-branch"
            @click.prevent="emit('attachCode')"
          >
            {{ t('repo.discussions.attachToCode') }}
          </UButton>

          <UFormField
            v-if="tags.length"
            :label="t('repo.discussions.fields.tags')"
          >
            <USelectMenu
              :model-value="tagIds"
              :items="tags"
              value-key="id"
              label-key="name"
              multiple
              @update:model-value="emit('update:tagIds', $event as string[])"
            />
          </UFormField>

          <UAlert
            v-if="error"
            color="error"
            variant="subtle"
            :description="error"
          />

          <div
            class="mt-auto flex justify-end gap-2 border-t pt-4"
            style="border-color: var(--ogb-border);"
          >
            <UButton
              variant="ghost"
              @click="emit('update:open', false)"
            >
              {{ t('common.cancel') }}
            </UButton>
            <UButton
              type="submit"
              :loading="creating"
            >
              {{ t('repo.discussions.createButton') }}
            </UButton>
          </div>
        </form>
      </aside>
    </div>
  </Teleport>
</template>
