import { describe, expect, it } from 'vitest'
import { resolveSafeRedirectPath } from './safeRedirect'

describe('resolveSafeRedirectPath', () => {
  it('returns allowed relative paths unchanged', () => {
    expect(resolveSafeRedirectPath('/settings')).toBe('/settings')
    expect(resolveSafeRedirectPath('/owner/repo/commit/abc123')).toBe('/owner/repo/commit/abc123')
  })

  it('falls back for missing or blank redirect', () => {
    expect(resolveSafeRedirectPath(undefined)).toBe('/')
    expect(resolveSafeRedirectPath(null)).toBe('/')
    expect(resolveSafeRedirectPath('')).toBe('/')
    expect(resolveSafeRedirectPath('   ')).toBe('/')
  })

  it('uses custom fallback when redirect is invalid', () => {
    expect(resolveSafeRedirectPath('//evil.com', '/dashboard')).toBe('/dashboard')
  })

  it('rejects protocol-relative and absolute URLs', () => {
    expect(resolveSafeRedirectPath('//evil.com')).toBe('/')
    expect(resolveSafeRedirectPath('https://evil.com')).toBe('/')
    expect(resolveSafeRedirectPath('http://evil.com')).toBe('/')
    expect(resolveSafeRedirectPath('/https://evil.com')).toBe('/')
  })

  it('rejects encoded open-redirect bypass attempts', () => {
    expect(resolveSafeRedirectPath('%2F%2Fevil.com')).toBe('/')
    expect(resolveSafeRedirectPath('%68%74%74%70%73%3a%2f%2fevil.com')).toBe('/')
    expect(resolveSafeRedirectPath('/%2F%2Fevil.com')).toBe('/')
  })

  it('accepts encoded safe paths', () => {
    expect(resolveSafeRedirectPath('%2Fsettings%2Fprofile')).toBe('/settings/profile')
  })
})
