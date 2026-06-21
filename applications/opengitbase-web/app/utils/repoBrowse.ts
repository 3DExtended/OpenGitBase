export function decodeRefParam(ref: string): string {
  return decodeURIComponent(ref)
}

export function encodeRefParam(ref: string): string {
  return encodeURIComponent(ref)
}

export function parsePathParam(path: string | string[] | undefined): string {
  if (!path) {
    return ''
  }
  const segments = Array.isArray(path) ? path : [path]
  return segments.map(segment => decodeURIComponent(segment)).join('/')
}

export function encodePathSegments(path: string): string {
  if (!path) {
    return ''
  }
  return path.split('/').map(segment => encodeURIComponent(segment)).join('/')
}

export function repoTreePath(owner: string, repo: string, ref: string, path = ''): string {
  const refSegment = encodeRefParam(ref)
  const base = `/${owner}/${repo}/tree/${refSegment}`
  if (!path) {
    return base
  }
  return `${base}/${encodePathSegments(path)}`
}

export function repoBlobPath(owner: string, repo: string, ref: string, path: string): string {
  const refSegment = encodeRefParam(ref)
  return `/${owner}/${repo}/blob/${refSegment}/${encodePathSegments(path)}`
}

export function repoHomePath(owner: string, repo: string): string {
  return `/${owner}/${repo}`
}

export function formatEntrySize(size: number | null | undefined): string {
  if (size == null) {
    return '—'
  }
  if (size < 1024) {
    return `${size} B`
  }
  if (size < 1024 * 1024) {
    return `${(size / 1024).toFixed(1)} KB`
  }
  return `${(size / (1024 * 1024)).toFixed(1)} MB`
}

export function fileNameFromPath(path: string): string {
  const segments = path.split('/')
  return segments[segments.length - 1] ?? path
}

export function isMarkdownPath(path: string): boolean {
  const lower = path.toLowerCase()
  return lower.endsWith('.md') || lower.endsWith('.markdown')
}
