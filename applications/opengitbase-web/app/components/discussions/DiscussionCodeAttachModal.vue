<script setup lang="ts">
import type { CommentAnchorInput } from '~/utils/api'

const props = defineProps<{
  open: boolean
  owner: string
  repoSlug: string
  defaultRef?: string | null
}>()

const emit = defineEmits<{
  'update:open': [value: boolean]
  select: [anchor: CommentAnchorInput]
}>()
</script>

<template>
  <CollaborationCodeAttachModal
    :open="open"
    :owner="owner"
    :repo-slug="repoSlug"
    :default-ref="defaultRef"
    @update:open="emit('update:open', $event)"
    @select="emit('select', $event)"
  />
</template>
