import { describe, expect, it } from 'vitest'

import { isReservedSlug, validateSlug } from './slug-validation'

const RESERVED_WEB_ROUTES = [
  '__visual__',
  'admin',
  'explore',
  'forgot-password',
  'gate',
  'invite',
  'orgs',
  'pitch',
  'repos',
  'reset-password',
  'settings',
  'sign-in',
  'sign-out',
  'sign-up',
  'status',
  'verify-email',
] as const

describe('slug-validation', () => {
  it.each(RESERVED_WEB_ROUTES)('reserves %s so it does not collide with /%s route', (slug) => {
    expect(isReservedSlug(slug)).toBe(true)
    if (slug === '__visual__') {
      // Underscores fail slug format before the reserved check runs.
      expect(validateSlug(slug)).toBe('slug.format')
      return
    }
    expect(validateSlug(slug)).toBe('slug.reserved')
  })

  it('is case-insensitive for reserved slugs', () => {
    expect(isReservedSlug('EXPLORE')).toBe(true)
  })

  it('allows normal usernames', () => {
    expect(validateSlug('validuser')).toBeNull()
  })
})
