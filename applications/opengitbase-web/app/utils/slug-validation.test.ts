import { describe, expect, it } from 'vitest'

import { isReservedSlug, validateSlug } from './slug-validation'

describe('slug-validation', () => {
  it('reserves pitch so it does not collide with /pitch route', () => {
    expect(isReservedSlug('pitch')).toBe(true)
    expect(validateSlug('pitch')).toBe('slug.reserved')
  })

  it('reserves status so it does not collide with /status route', () => {
    expect(isReservedSlug('status')).toBe(true)
    expect(validateSlug('status')).toBe('slug.reserved')
  })
})
