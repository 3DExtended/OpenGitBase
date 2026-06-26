export type MarkdownSegment =
  | { type: 'prose', markdown: string }
  | { type: 'code', language: string, source: string }

/** Split markdown into prose sections and fenced code blocks. */
export function splitMarkdownByFences(source: string): MarkdownSegment[] {
  if (!source) {
    return []
  }

  const segments: MarkdownSegment[] = []
  const lines = source.split('\n')
  const proseBuffer: string[] = []
  let index = 0

  function flushProse(): void {
    if (!proseBuffer.length) {
      return
    }
    segments.push({ type: 'prose', markdown: proseBuffer.join('\n') })
    proseBuffer.length = 0
  }

  while (index < lines.length) {
    const line = lines[index]!
    if (line.startsWith('```')) {
      flushProse()
      const language = line.slice(3).trim()
      index += 1
      const codeLines: string[] = []
      while (index < lines.length && !lines[index]!.startsWith('```')) {
        codeLines.push(lines[index]!)
        index += 1
      }
      segments.push({
        type: 'code',
        language,
        source: codeLines.join('\n'),
      })
      if (index < lines.length) {
        index += 1
      }
      continue
    }

    proseBuffer.push(line)
    index += 1
  }

  flushProse()

  if (!segments.length && source.trim()) {
    segments.push({ type: 'prose', markdown: source })
  }

  return segments
}
