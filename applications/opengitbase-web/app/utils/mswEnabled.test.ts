import { describe, expect, it } from 'vitest'
import { resolveMswEnabled } from './mswEnabled'

describe('resolveMswEnabled', () => {
  it('returns false in production even when config is true', () => {
    expect(resolveMswEnabled({
      isDev: false,
      mswPublicConfig: 'true',
      hasMswQuery: true,
    })).toBe(false)
  })

  it('returns true in dev when public config is true', () => {
    expect(resolveMswEnabled({
      isDev: true,
      mswPublicConfig: 'true',
      hasMswQuery: false,
    })).toBe(true)
  })

  it('returns true in dev when ?msw=1 is present', () => {
    expect(resolveMswEnabled({
      isDev: true,
      mswPublicConfig: 'false',
      hasMswQuery: true,
    })).toBe(true)
  })

  it('returns false in dev when config is false and no query flag', () => {
    expect(resolveMswEnabled({
      isDev: true,
      mswPublicConfig: 'false',
      hasMswQuery: false,
    })).toBe(false)
  })
})
