const RESERVED_SLUGS = new Set([
  'explore',
  'pitch',
  'sign-in',
  'sign-up',
  'settings',
  'api',
  'register',
  'health',
  'swagger',
  '__visual__',
  'forgot-password',
  'reset-password',
  'verify-email',
  'sign-out',
  'orgs',
  'repos',
])

const SLUG_PATTERN = /^[a-z0-9](?:[a-z0-9-]*[a-z0-9])?$/i

export function isReservedSlug(slug: string): boolean {
  return RESERVED_SLUGS.has(slug.trim().toLowerCase())
}

export function validateSlug(slug: string): string | null {
  const trimmed = slug.trim()
  if (!trimmed) {
    return 'slug.required'
  }
  if (trimmed.length < 2 || trimmed.length > 39) {
    return 'slug.length'
  }
  if (!SLUG_PATTERN.test(trimmed)) {
    return 'slug.format'
  }
  if (isReservedSlug(trimmed)) {
    return 'slug.reserved'
  }
  return null
}

export function slugify(name: string): string {
  return name
    .trim()
    .replace(/([a-z0-9])([A-Z])/g, '$1-$2')
    .replace(/([A-Z])([A-Z][a-z])/g, '$1-$2')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, 39)
}
