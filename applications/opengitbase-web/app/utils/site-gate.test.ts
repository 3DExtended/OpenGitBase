/** @vitest-environment jsdom */
import { beforeEach, describe, expect, it } from 'vitest'
import { isSiteGateUnlocked, unlockSiteGate } from './site-gate'

function clearCookies() {
  for (const part of document.cookie.split(';')) {
    const name = part.split('=')[0]?.trim()
    if (name) {
      document.cookie = `${name}=; Max-Age=0; Path=/`
    }
  }
}

describe('site gate', () => {
  beforeEach(() => {
    clearCookies()
  })

  it('unlocks with OpenGitBase and remembers via session cookie', () => {
    expect(isSiteGateUnlocked()).toBe(false)
    expect(unlockSiteGate('OpenGitBase')).toBe(true)
    expect(isSiteGateUnlocked()).toBe(true)
    expect(document.cookie).toContain('ogb-site-gate-unlocked=1')
  })

  it('rejects the wrong password', () => {
    expect(unlockSiteGate('wrong')).toBe(false)
    expect(isSiteGateUnlocked()).toBe(false)
  })
})
