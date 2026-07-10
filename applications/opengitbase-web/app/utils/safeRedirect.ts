const DEFAULT_FALLBACK = '/'

function decodeRedirectCandidate(value: string): string | null {
  try {
    return decodeURIComponent(value)
  }
  catch {
    return null
  }
}

function isSameOriginRelativePath(path: string): boolean {
  if (!path.startsWith('/')) {
    return false
  }

  const normalized = path.replace(/\\/g, '/')
  if (normalized.startsWith('//')) {
    return false
  }

  if (normalized.includes('://')) {
    return false
  }

  const lower = normalized.toLowerCase()
  if (lower.startsWith('http:') || lower.startsWith('https:')) {
    return false
  }

  const schemeIndex = normalized.indexOf(':')
  if (schemeIndex > 0) {
    const scheme = normalized.slice(0, schemeIndex).toLowerCase()
    if (/^[a-z][a-z0-9+.-]*$/.test(scheme)) {
      return false
    }
  }

  return true
}

/**
 * Accept only same-origin relative paths for post-auth redirects.
 * Rejects protocol-relative, absolute, and encoded bypass attempts.
 */
export function resolveSafeRedirectPath(
  redirect: string | null | undefined,
  fallback: string = DEFAULT_FALLBACK,
): string {
  if (typeof redirect !== 'string') {
    return fallback
  }

  const trimmed = redirect.trim()
  if (!trimmed) {
    return fallback
  }

  const decoded = decodeRedirectCandidate(trimmed) ?? trimmed
  if (!isSameOriginRelativePath(decoded)) {
    return fallback
  }

  return decoded
}
