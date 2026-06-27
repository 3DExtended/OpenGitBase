<script setup lang="ts">
import type { CollaborationFlatCommentProps } from '~/components/collaboration/types'
import CollaborationMarkdownEditor from '~/components/collaboration/CollaborationMarkdownEditor.vue'
import CollaborationRenderedBody from '~/components/collaboration/CollaborationRenderedBody.vue'

const props = withDefaults(defineProps<CollaborationFlatCommentProps>(), {
  editedLabel: 'Edited',
})

const emit = defineEmits<{
  edit: [body: string]
  delete: []
}>()

const { t } = useI18n()
const editing = ref(false)
const editBody = ref('')

function startEdit(): void {
  editBody.value = props.bodyMarkdown
  editing.value = true
}

function cancelEdit(): void {
  editing.value = false
  editBody.value = ''
}

function submitEdit(): void {
  const body = editBody.value.trim()
  if (!body) {
    return
  }
  emit('edit', body)
  editing.value = false
}
</script>

<template>
  <article
    :id="`comment-${id}`"
    class="scroll-mt-24 rounded-lg border px-3 py-3"
    style="border-color: var(--ogb-border);"
    data-testid="collaboration-flat-comment"
  >
    <header class="mb-2 flex flex-wrap items-center justify-between gap-2 text-xs text-[var(--ogb-text-muted)]">
      <div class="flex flex-wrap items-center gap-2">
        <span class="font-medium text-[var(--ogb-text)]">{{ memberLabel(author.userId, author.username) }}</span>
        <span><RelativeTime :iso="createdAt" /></span>
        <span
          v-if="editedAt"
          class="text-[var(--ogb-text-muted)]"
        >
          · {{ editedLabel }}
        </span>
      </div>
      <div
        v-if="canEdit || canDelete"
        class="flex items-center gap-1"
      >
        <UButton
          v-if="canEdit && !editing"
          size="xs"
          variant="ghost"
          icon="i-lucide-pencil"
          @click="startEdit"
        >
          {{ t('common.edit') }}
        </UButton>
        <UButton
          v-if="canDelete && !editing"
          size="xs"
          variant="ghost"
          color="error"
          icon="i-lucide-trash-2"
          @click="emit('delete')"
        >
          {{ t('common.delete') }}
        </UButton>
      </div>
    </header>

    <form
      v-if="editing"
      class="space-y-2"
      @submit.prevent="submitEdit"
    >
      <CollaborationMarkdownEditor
        v-model="editBody"
        min-height="4rem"
      />
      <div class="flex gap-2">
        <UButton
          type="submit"
          size="xs"
          :disabled="!editBody.trim()"
        >
          {{ t('common.save') }}
        </UButton>
        <UButton
          type="button"
          size="xs"
          variant="ghost"
          @click="cancelEdit"
        >
          {{ t('common.cancel') }}
        </UButton>
      </div>
    </form>
    <CollaborationRenderedBody
      v-else
      :source="bodyMarkdown"
    />
  </article>
</template>
