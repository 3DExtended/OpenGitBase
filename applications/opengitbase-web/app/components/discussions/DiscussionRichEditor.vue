<script setup lang="ts">
// @ts-nocheck — tiptap extension packages pin mixed @tiptap/core minors; runtime is fine.
import Link from '@tiptap/extension-link'
import Placeholder from '@tiptap/extension-placeholder'
import Strike from '@tiptap/extension-strike'
import { Table } from '@tiptap/extension-table'
import TableCell from '@tiptap/extension-table-cell'
import TableHeader from '@tiptap/extension-table-header'
import TableRow from '@tiptap/extension-table-row'
import TaskItem from '@tiptap/extension-task-item'
import TaskList from '@tiptap/extension-task-list'
import Underline from '@tiptap/extension-underline'
import StarterKit from '@tiptap/starter-kit'
import { EditorContent, useEditor } from '@tiptap/vue-3'
import { Markdown } from 'tiptap-markdown'
import type { RepositoryMember } from '~/utils/api'

type RichEditor = NonNullable<ReturnType<typeof useEditor>['value']>

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

const mentionOpen = ref(false)
const mentionQuery = ref('')
const mentionRange = ref<{ from: number, to: number } | null>(null)

const filteredMembers = computed(() => {
  const q = mentionQuery.value.trim().toLowerCase()
  const list = props.members.filter(m => m.username)
  if (!q) {
    return list.slice(0, 8)
  }
  return list.filter(m => m.username!.toLowerCase().includes(q)).slice(0, 8)
})

function markdownFromEditor(ed: RichEditor): string {
  const storage = ed.storage as unknown as { markdown?: { getMarkdown?: () => string } }
  return storage.markdown?.getMarkdown?.() ?? ed.getText()
}

const editor = useEditor({
  extensions: [
    StarterKit.configure({
      heading: { levels: [2, 3] },
    }),
    Underline,
    Strike,
    Link.configure({ openOnClick: false }),
    Table.configure({ resizable: false }),
    TableRow,
    TableHeader,
    TableCell,
    TaskList,
    TaskItem.configure({ nested: true }),
    Placeholder.configure({ placeholder: props.placeholder }),
    Markdown.configure({
      html: false,
      transformPastedText: true,
      transformCopiedText: true,
    }),
  ] as const,
  content: props.modelValue,
  immediatelyRender: false,
  editorProps: {
    attributes: {
      class: 'discussion-rich-editor__content max-w-none focus:outline-none',
    },
  },
  onUpdate: ({ editor: ed }) => {
    emit('update:modelValue', markdownFromEditor(ed))
    detectMentionTrigger(ed)
  },
})

function detectMentionTrigger(ed: RichEditor): void {
  const { from } = ed.state.selection
  const lookbehind = ed.state.doc.textBetween(Math.max(0, from - 32), from, '\n', '\n')
  const match = lookbehind.match(/(?:^|\s)@([\w-]*)$/)
  if (!match) {
    mentionOpen.value = false
    return
  }
  mentionQuery.value = match[1] ?? ''
  mentionRange.value = { from: from - (match[1]?.length ?? 0) - 1, to: from }
  mentionOpen.value = props.members.length > 0
}

function insertMention(member: RepositoryMember): void {
  const ed = editor.value
  const range = mentionRange.value
  if (!ed || !range || !member.userId) {
    return
  }
  ed
    .chain()
    .focus()
    .deleteRange(range)
    .insertContent(`@{${member.userId}} `)
    .run()
  mentionOpen.value = false
}

function run(action: (ed: RichEditor) => void): void {
  const ed = editor.value
  if (ed) {
    action(ed)
  }
}

function promptLink(): void {
  const url = window.prompt('URL')
  if (url) {
    run(ed => ed.chain().focus().setLink({ href: url }).run())
  }
}

type ToolbarAction = {
  label: string
  icon?: string
  run: (ed: RichEditor) => void
}

const toolbarActions: ToolbarAction[] = [
  { label: 'Bold', icon: 'i-lucide-bold', run: ed => ed.chain().focus().toggleBold().run() },
  { label: 'Italic', icon: 'i-lucide-italic', run: ed => ed.chain().focus().toggleItalic().run() },
  { label: 'Underline', icon: 'i-lucide-underline', run: ed => ed.chain().focus().toggleUnderline().run() },
  { label: 'Strikethrough', icon: 'i-lucide-strikethrough', run: ed => ed.chain().focus().toggleStrike().run() },
  { label: 'Inline code', icon: 'i-lucide-code', run: ed => ed.chain().focus().toggleCode().run() },
  { label: 'Link', icon: 'i-lucide-link', run: () => promptLink() },
  { label: 'Bullet list', icon: 'i-lucide-list', run: ed => ed.chain().focus().toggleBulletList().run() },
  { label: 'Numbered list', icon: 'i-lucide-list-ordered', run: ed => ed.chain().focus().toggleOrderedList().run() },
  { label: 'Blockquote', icon: 'i-lucide-text-quote', run: ed => ed.chain().focus().toggleBlockquote().run() },
  { label: 'Heading 2', run: ed => ed.chain().focus().toggleHeading({ level: 2 }).run() },
  { label: 'Heading 3', run: ed => ed.chain().focus().toggleHeading({ level: 3 }).run() },
  { label: 'Code block', icon: 'i-lucide-square-code', run: ed => ed.chain().focus().toggleCodeBlock().run() },
  { label: 'Task list', icon: 'i-lucide-list-todo', run: ed => ed.chain().focus().toggleTaskList().run() },
  { label: 'Table', icon: 'i-lucide-table', run: ed => ed.chain().focus().insertTable({ rows: 2, cols: 2, withHeaderRow: true }).run() },
]

function onToolbarAction(action: ToolbarAction): void {
  run(action.run)
}

watch(
  () => props.modelValue,
  (value) => {
    const ed = editor.value
    if (!ed) {
      return
    }
    const current = markdownFromEditor(ed)
    if (value !== current) {
      ed.commands.setContent(value)
    }
  },
)

onBeforeUnmount(() => {
  editor.value?.destroy()
})
</script>

<template>
  <ClientOnly>
    <div
      class="discussion-rich-editor overflow-hidden rounded-lg border"
      style="border-color: var(--ogb-border); background: var(--ogb-surface);"
    >
      <div
        class="discussion-rich-editor__toolbar flex items-center gap-0.5 border-b px-2"
        style="border-color: var(--ogb-border);"
        role="toolbar"
        :aria-label="placeholder || 'Formatting toolbar'"
      >
        <button
          v-for="action in toolbarActions"
          :key="action.label"
          type="button"
          class="discussion-rich-editor__tool"
          :class="{ 'discussion-rich-editor__tool--label': !action.icon }"
          :title="action.label"
          :aria-label="action.label"
          @click="onToolbarAction(action)"
        >
          <UIcon
            v-if="action.icon"
            :name="action.icon"
            class="size-4 shrink-0"
          />
          <span
            v-else
            class="text-xs font-medium leading-none"
          >{{ action.label === 'Heading 2' ? 'H2' : 'H3' }}</span>
        </button>
      </div>

      <div class="discussion-rich-editor__body relative">
        <EditorContent :editor="editor" />
        <div
          v-if="mentionOpen && filteredMembers.length"
          class="absolute bottom-2 left-2 z-10 max-h-40 w-56 overflow-y-auto rounded-md border bg-[var(--ogb-surface)] shadow-lg"
          style="border-color: var(--ogb-border);"
        >
          <button
            v-for="member in filteredMembers"
            :key="member.userId"
            type="button"
            class="flex w-full items-center gap-2 px-3 py-2 text-left text-sm hover:bg-[var(--ogb-bg)]"
            @click="insertMention(member)"
          >
            <span class="flex size-6 items-center justify-center rounded-full bg-teal-600 text-xs text-white">
              {{ (member.username ?? '?').slice(0, 1).toUpperCase() }}
            </span>
            {{ member.username }}
          </button>
        </div>
      </div>
    </div>

    <template #fallback>
      <UTextarea
        :model-value="modelValue"
        :rows="4"
        :placeholder="placeholder"
        @update:model-value="emit('update:modelValue', $event)"
      />
    </template>
  </ClientOnly>
</template>

<style scoped>
.discussion-rich-editor__toolbar {
  height: 1.75rem;
  flex-shrink: 0;
  overflow-x: auto;
  overflow-y: hidden;
  flex-wrap: nowrap;
}

.discussion-rich-editor__tool {
  display: inline-flex;
  height: 1.75rem;
  width: 1.75rem;
  flex-shrink: 0;
  align-items: center;
  justify-content: center;
  border-radius: 0.25rem;
  border: 0;
  padding: 0;
  background: transparent;
  color: var(--ogb-accent);
  cursor: pointer;
}

.discussion-rich-editor__tool:hover {
  background: color-mix(in srgb, var(--ogb-accent) 12%, transparent);
}

.discussion-rich-editor__tool--label {
  width: auto;
  min-width: 1.75rem;
  padding-inline: 0.375rem;
}

.discussion-rich-editor__body {
  min-height: v-bind(minHeight);
}

.discussion-rich-editor :deep(.tiptap),
.discussion-rich-editor :deep(.ProseMirror) {
  min-height: inherit;
}

.discussion-rich-editor :deep(.ProseMirror) {
  padding: 0.75rem;
  outline: none;
}

.discussion-rich-editor :deep(.ProseMirror > *:first-child) {
  margin-top: 0;
}

.discussion-rich-editor :deep(.ProseMirror p) {
  margin: 0;
}

.discussion-rich-editor :deep(.ProseMirror p.is-editor-empty:first-child::before) {
  color: var(--ogb-text-muted);
  content: attr(data-placeholder);
  float: left;
  height: 0;
  pointer-events: none;
}
</style>
