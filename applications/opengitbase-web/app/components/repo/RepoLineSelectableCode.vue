<script setup lang="ts">
const props = defineProps<{
  source: string
  path: string
}>()

const emit = defineEmits<{
  rangeSelected: [payload: { line: number, endLine: number | null }]
}>()

const { t } = useI18n()

const {
  pickStep,
  confirmedRange,
  resetPick,
  onLineHover,
  onLineClick,
  lineClass,
  previewRange,
} = useLineRangePick((range) => {
  emit('rangeSelected', range)
})

const displayLines = computed(() => props.source.split('\n'))

const rangeLabel = computed(() => {
  const range = confirmedRange.value ?? previewRange()
  if (!range) {
    return null
  }
  return range.endLine && range.endLine !== range.line
    ? `${range.line}–${range.endLine}`
    : `${range.line}`
})

defineExpose({ resetPick })
</script>

<template>
  <div class="space-y-2">
    <p class="text-xs text-[var(--ogb-text-muted)]">
      <span v-if="confirmedRange">{{ t('repo.discussions.lineRangeSelected', { range: rangeLabel }) }}</span>
      <span v-else-if="pickStep === 'start'">{{ t('repo.discussions.pickStartLine') }}</span>
      <span v-else>{{ t('repo.discussions.pickEndLine') }}</span>
    </p>
    <div
      class="max-h-[32rem] overflow-auto rounded-md border font-mono text-xs leading-relaxed"
      style="border-color: var(--ogb-border); background: var(--ogb-bg);"
      @mouseleave="onLineHover(null)"
    >
      <button
        v-for="(line, index) in displayLines"
        :key="index"
        type="button"
        class="flex w-full gap-3 px-2 py-0.5 text-left hover:bg-[var(--ogb-surface)]"
        :class="lineClass(index + 1)"
        @mouseenter="onLineHover(index + 1)"
        @click="onLineClick(index + 1)"
      >
        <span class="w-10 shrink-0 select-none text-right text-[var(--ogb-text-muted)]">{{ index + 1 }}</span>
        <span class="min-w-0 whitespace-pre-wrap">{{ line || ' ' }}</span>
      </button>
    </div>
  </div>
</template>
