<script setup lang="ts">
import type { CommentAnchorInput } from '~/utils/api'
import { repoTreePath } from '~/utils/repoBrowse'

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

const api = useApi()
const { t } = useI18n()

const refs = ref<{ branches: Array<{ name: string, commitSha: string }>, tags: Array<{ name: string, commitSha: string }> } | null>(null)
const refName = ref('')
const currentPath = ref('')
const treeEntries = ref<Array<{ name: string, path: string, type: string }>>([])
const blobText = ref<string | null>(null)
const blobPath = ref('')
const modalStep = ref<'browse' | 'pick'>('browse')
const loading = ref(false)

const {
  pickStep: linePickStep,
  confirmedRange,
  onLineHover,
  onLineClick,
  lineClass,
  previewRange,
  resetPick,
} = useLineRangePick()

const refOptions = computed(() => {
  if (!refs.value) {
    return []
  }
  return [
    ...refs.value.branches.map(b => ({ label: b.name, value: b.name, sha: b.commitSha })),
    ...refs.value.tags.map(tag => ({ label: tag.name, value: tag.name, sha: tag.commitSha })),
  ]
})

function commitShaForRef(name: string): string {
  const match = refOptions.value.find(r => r.value === name)
  return match?.sha ?? ''
}

async function loadRefs(): Promise<void> {
  loading.value = true
  const result = await api.repositoryContent.getRefs(props.owner, props.repoSlug)
  if (result.data) {
    refs.value = result.data
    refName.value = props.defaultRef ?? result.data.defaultRef ?? result.data.branches[0]?.name ?? 'main'
    await loadTree('')
  }
  loading.value = false
}

async function loadTree(path: string): Promise<void> {
  currentPath.value = path
  blobText.value = null
  modalStep.value = 'browse'
  resetPick()
  const result = await api.repositoryContent.getTree(props.owner, props.repoSlug, refName.value, path)
  treeEntries.value = result.data?.entries ?? []
}

async function openFile(path: string): Promise<void> {
  loading.value = true
  blobPath.value = path
  const result = await api.repositoryContent.getBlob(props.owner, props.repoSlug, refName.value, path)
  blobText.value = result.data?.textContent ?? null
  modalStep.value = 'pick'
  resetPick()
  loading.value = false
}

const displayLines = computed(() => blobText.value?.split('\n') ?? [])

const rangeLabel = computed(() => {
  const range = confirmedRange.value ?? previewRange()
  if (!range) {
    return null
  }
  return range.endLine && range.endLine !== range.line
    ? `${range.line}–${range.endLine}`
    : `${range.line}`
})

function confirmSelection(): void {
  const range = confirmedRange.value
  if (!range) {
    return
  }
  emit('select', {
    ref: refName.value,
    commitSha: commitShaForRef(refName.value),
    filePath: blobPath.value,
    line: range.line,
    endLine: range.endLine,
  })
  resetPick()
  emit('update:open', false)
}

function onLineClickInModal(lineNumber: number): void {
  onLineClick(lineNumber)
  if (confirmedRange.value) {
    confirmSelection()
  }
}

watch(
  () => props.open,
  (isOpen) => {
    if (isOpen) {
      void loadRefs()
    }
    else {
      resetPick()
      modalStep.value = 'browse'
    }
  },
)

watch(refName, () => {
  void loadTree('')
})
</script>

<template>
  <Teleport to="body">
    <div
      v-if="open"
      class="fixed inset-0 z-[200] flex items-center justify-center bg-black/40 p-4"
      @click.self="emit('update:open', false)"
    >
      <UCard class="max-h-[80vh] w-full max-w-2xl overflow-hidden">
        <template #header>
          <div class="flex items-center justify-between gap-2">
            <div>
              <h3 class="font-semibold">
                {{ t('repo.discussions.attachModalTitle') }}
              </h3>
              <p class="text-xs text-[var(--ogb-text-muted)]">
                <span v-if="modalStep === 'pick' && linePickStep === 'start'">{{ t('repo.discussions.pickStartLine') }}</span>
                <span v-else-if="modalStep === 'pick'">{{ t('repo.discussions.pickEndLine') }}</span>
                <span v-else>{{ t('repo.discussions.browseFile') }}</span>
              </p>
            </div>
            <UButton
              variant="ghost"
              icon="i-lucide-x"
              size="sm"
              @click="emit('update:open', false)"
            />
          </div>
        </template>

        <div class="space-y-3 overflow-y-auto">
          <USelect
            v-model="refName"
            :items="refOptions"
            value-key="value"
            label-key="label"
            class="min-w-48"
          />

          <div
            v-if="modalStep === 'browse'"
            class="space-y-1"
          >
            <NuxtLink
              v-if="currentPath"
              :to="repoTreePath(owner, repoSlug, refName, currentPath.split('/').slice(0, -1).join('/'))"
              class="text-xs text-[var(--ogb-accent)]"
              @click.prevent="loadTree(currentPath.split('/').slice(0, -1).join('/'))"
            >
              ↑ up
            </NuxtLink>
            <button
              v-for="entry in treeEntries"
              :key="entry.path"
              type="button"
              class="flex w-full items-center gap-2 rounded px-2 py-1.5 text-left text-sm hover:bg-[var(--ogb-bg)]"
              @click="entry.type === 'tree' ? loadTree(entry.path) : openFile(entry.path)"
            >
              <span>{{ entry.type === 'tree' ? '📁' : '📄' }}</span>
              {{ entry.name }}
            </button>
          </div>

          <template v-else>
            <p
              v-if="rangeLabel"
              class="text-xs text-[var(--ogb-text-muted)]"
            >
              {{ t('repo.discussions.lineRangePreview', { range: rangeLabel }) }}
            </p>
            <div
              class="max-h-96 overflow-auto rounded border font-mono text-xs"
              style="border-color: var(--ogb-border);"
              @mouseleave="onLineHover(null)"
            >
              <button
                v-for="(line, index) in displayLines"
                :key="index"
                type="button"
                class="flex w-full gap-3 px-2 py-0.5 text-left hover:bg-[var(--ogb-bg)]"
                :class="lineClass(index + 1)"
                @mouseenter="onLineHover(index + 1)"
                @click="onLineClickInModal(index + 1)"
              >
                <span class="w-8 shrink-0 text-right text-[var(--ogb-text-muted)]">{{ index + 1 }}</span>
                <span class="min-w-0 whitespace-pre-wrap">{{ line || ' ' }}</span>
              </button>
            </div>
          </template>
        </div>
      </UCard>
    </div>
  </Teleport>
</template>
