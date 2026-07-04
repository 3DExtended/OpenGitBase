<script setup lang="ts">
import type { DiffSide, MergeRequestDiffFile, MergeRequestDiffLine } from '~/utils/api'

defineProps<{
  files: MergeRequestDiffFile[]
  readOnly?: boolean
  emptyLabel?: string
}>()

const emit = defineEmits<{
  lineSelect: [payload: {
    filePath: string
    lineNumber: number
    diffSide: DiffSide
    line: MergeRequestDiffLine
  }]
}>()

function diffSideForLine(line: MergeRequestDiffLine): DiffSide {
  return line.type === 'remove' ? 'old' : 'new'
}

function lineNumberForSelection(line: MergeRequestDiffLine): number {
  return line.newLineNumber ?? line.oldLineNumber ?? 0
}
</script>

<template>
  <div class="space-y-4">
    <UCard v-if="!files.length">
      <p class="text-sm text-[var(--ogb-text-muted)]">
        {{ emptyLabel ?? 'No changes.' }}
      </p>
    </UCard>
    <UCard
      v-for="file in files"
      :key="file.filePath"
      class="overflow-hidden"
    >
      <template #header>
        <div class="flex items-center justify-between gap-3">
          <p class="font-mono text-sm">
            {{ file.filePath }}
          </p>
          <UBadge
            variant="subtle"
            color="neutral"
          >
            {{ file.changeType }}
          </UBadge>
        </div>
      </template>
      <div class="space-y-4">
        <div
          v-for="hunk in file.hunks"
          :key="hunk.header"
          class="overflow-hidden rounded border"
          style="border-color: var(--ogb-border);"
        >
          <p
            class="border-b px-3 py-2 font-mono text-xs text-[var(--ogb-text-muted)]"
            style="border-color: var(--ogb-border);"
          >
            {{ hunk.header }}
          </p>
          <div class="font-mono text-xs">
            <div
              v-for="(line, idx) in hunk.lines"
              :key="`${hunk.header}-${idx}`"
              class="border-b px-3 py-1"
              style="border-color: color-mix(in srgb, var(--ogb-border) 50%, transparent);"
              :class="{
                'bg-emerald-500/10': line.type === 'add',
                'bg-rose-500/10': line.type === 'remove',
              }"
            >
              <div class="flex items-start gap-2">
                <slot
                  name="line-prefix"
                  :file="file"
                  :line="line"
                  :hunk="hunk"
                >
                  <button
                    v-if="!readOnly && (line.newLineNumber || line.oldLineNumber)"
                    type="button"
                    class="text-[10px] text-[var(--ogb-text-muted)] hover:text-[var(--ogb-text)]"
                    @click="emit('lineSelect', {
                      filePath: file.filePath,
                      lineNumber: lineNumberForSelection(line),
                      diffSide: diffSideForLine(line),
                      line,
                    })"
                  >
                    +
                  </button>
                </slot>
                <span class="w-8 text-right text-[var(--ogb-text-muted)]">{{ line.oldLineNumber ?? '' }}</span>
                <span class="w-8 text-right text-[var(--ogb-text-muted)]">{{ line.newLineNumber ?? '' }}</span>
                <span class="min-w-0 flex-1 whitespace-pre-wrap">{{ line.content }}</span>
              </div>

              <slot
                name="line-threads"
                :file="file"
                :line="line"
                :hunk="hunk"
                :diff-side="diffSideForLine(line)"
                :line-number="lineNumberForSelection(line)"
              />
            </div>
          </div>
        </div>
      </div>
    </UCard>
  </div>
</template>
