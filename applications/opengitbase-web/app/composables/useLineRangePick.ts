export type LineRange = {
  line: number
  endLine: number | null
}

export function useLineRangePick(onComplete?: (range: LineRange) => void) {
  const pickStep = ref<'start' | 'end'>('start')
  const startLine = ref<number | null>(null)
  const hoverLine = ref<number | null>(null)
  const confirmedRange = ref<LineRange | null>(null)

  function resetPick(): void {
    pickStep.value = 'start'
    startLine.value = null
    hoverLine.value = null
    confirmedRange.value = null
  }

  function onLineHover(lineNumber: number | null): void {
    hoverLine.value = lineNumber
  }

  function onLineClick(lineNumber: number): void {
    if (pickStep.value === 'start') {
      startLine.value = lineNumber
      hoverLine.value = lineNumber
      pickStep.value = 'end'
      return
    }
    if (startLine.value === null) {
      return
    }
    const line = Math.min(startLine.value, lineNumber)
    const end = Math.max(startLine.value, lineNumber)
    const range: LineRange = {
      line,
      endLine: end === line ? null : end,
    }
    confirmedRange.value = range
    onComplete?.(range)
  }

  function lineClass(lineNumber: number): string {
    const range = confirmedRange.value ?? previewRange()
    if (!range) {
      return ''
    }
    const lo = range.line
    const hi = range.endLine ?? range.line
    if (lineNumber >= lo && lineNumber <= hi) {
      return 'bg-teal-500/20'
    }
    return ''
  }

  function previewRange(): LineRange | null {
    if (startLine.value === null) {
      return null
    }
    const end = hoverLine.value ?? startLine.value
    return {
      line: Math.min(startLine.value, end),
      endLine: Math.max(startLine.value, end) === Math.min(startLine.value, end)
        ? null
        : Math.max(startLine.value, end),
    }
  }

  return {
    pickStep,
    startLine,
    hoverLine,
    confirmedRange,
    resetPick,
    onLineHover,
    onLineClick,
    lineClass,
    previewRange,
  }
}
