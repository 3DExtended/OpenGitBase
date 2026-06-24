export type LineRange = {
  line: number
  endLine: number | null
}

export function useLineRangePick(
  onComplete?: (range: LineRange) => void,
  onClear?: () => void,
) {
  const hoverLine = ref<number | null>(null)
  const confirmedRange = ref<LineRange | null>(null)

  function resetPick(): void {
    hoverLine.value = null
    confirmedRange.value = null
  }

  function clearSelection(): void {
    resetPick()
    onClear?.()
  }

  function onLineHover(lineNumber: number | null): void {
    hoverLine.value = lineNumber
  }

  function applyRange(range: LineRange): void {
    confirmedRange.value = range
    onComplete?.(range)
  }

  function onLineClick(lineNumber: number): void {
    if (confirmedRange.value !== null) {
      const lo = confirmedRange.value.line
      const hi = confirmedRange.value.endLine ?? confirmedRange.value.line

      if (lineNumber >= lo && lineNumber <= hi) {
        clearSelection()
        return
      }

      const line = Math.min(lo, lineNumber)
      const end = Math.max(lo, lineNumber)
      applyRange({
        line,
        endLine: end === line ? null : end,
      })
      return
    }

    applyRange({
      line: lineNumber,
      endLine: null,
    })
  }

  function lineClass(lineNumber: number): string {
    const range = previewRange()
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
    if (confirmedRange.value !== null) {
      const cur = confirmedRange.value
      if (cur.endLine === null && hoverLine.value !== null && hoverLine.value !== cur.line) {
        const line = Math.min(cur.line, hoverLine.value)
        const end = Math.max(cur.line, hoverLine.value)
        return {
          line,
          endLine: end === line ? null : end,
        }
      }
      return cur
    }

    if (hoverLine.value !== null) {
      return {
        line: hoverLine.value,
        endLine: null,
      }
    }

    return null
  }

  return {
    confirmedRange,
    hoverLine,
    resetPick,
    clearSelection,
    onLineHover,
    onLineClick,
    lineClass,
    previewRange,
  }
}
