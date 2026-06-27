<script setup lang="ts">
import type { RepositoryMember } from '~/utils/api'

const props = withDefaults(
  defineProps<{
    modelValue: string
    placeholder?: string
    members?: RepositoryMember[]
    minHeight?: string
  }>(),
  {
    placeholder: '',
    members: () => [],
    minHeight: '8rem',
  },
)

const emit = defineEmits<{
  'update:modelValue': [value: string]
}>()
</script>

<template>
  <CollaborationMarkdownEditor
    :model-value="modelValue"
    :placeholder="placeholder"
    :members="members"
    :min-height="minHeight"
    @update:model-value="emit('update:modelValue', $event)"
  />
</template>
