// Keep in sync with common/OpenGitBase.Common/Services/ReservedSlugValidator.cs
const RESERVED_SLUGS = new Set([
  '__visual__',
  'admin',
  'api',
  'explore',
  'forgot-password',
  'gate',
  'health',
  'invite',
  'opengitbase',
  'orgs',
  'pitch',
  'register',
  'repos',
  'reset-password',
  'settings',
  'sign-in',
  'sign-out',
  'sign-up',
  'status',
  'swagger',
  'verify-email',
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
