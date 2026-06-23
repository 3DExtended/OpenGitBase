import type { CommentAnchorInput } from '~/utils/api'
import type { LocationQuery, LocationQueryValue } from 'vue-router'

export function anchorToQuery(anchor: CommentAnchorInput): Record<string, string> {
  const query: Record<string, string> = {
    anchorRef: anchor.ref,
    anchorSha: anchor.commitSha,
    anchorPath: anchor.filePath,
    anchorLine: String(anchor.line),
  }
  if (anchor.endLine != null && anchor.endLine !== anchor.line) {
    query.anchorEndLine = String(anchor.endLine)
  }
  return query
}

export function parseAnchorFromRouteQuery(query: LocationQuery): CommentAnchorInput | null {
  const ref = firstQueryValue(query.anchorRef)
  const path = firstQueryValue(query.anchorPath)
  const lineRaw = firstQueryValue(query.anchorLine)
  if (!ref || !path || !lineRaw) {
    return null
  }
  const line = Number(lineRaw)
  if (!Number.isFinite(line) || line < 1) {
    return null
  }
  const endRaw = firstQueryValue(query.anchorEndLine)
  const endLine = endRaw ? Number(endRaw) : null
  return {
    ref,
    commitSha: firstQueryValue(query.anchorSha) ?? '',
    filePath: path,
    line,
    endLine: endLine != null && Number.isFinite(endLine) && endLine !== line ? endLine : null,
  }
}

function firstQueryValue(value: LocationQueryValue | LocationQueryValue[] | undefined): string | undefined {
  if (value == null) {
    return undefined
  }
  if (Array.isArray(value)) {
    return value[0] ?? undefined
  }
  return value
}
