const STORAGE_KEY = 'ogb-site-gate-unlocked'

export const SITE_GATE_PASSWORD = 'OpenGitBase'

export function isSiteGateUnlocked(): boolean {
  if (!import.meta.client) {
    return false
  }

  return localStorage.getItem(STORAGE_KEY) === '1'
}

export function unlockSiteGate(password: string): boolean {
  if (password !== SITE_GATE_PASSWORD) {
    return false
  }

  if (import.meta.client) {
    localStorage.setItem(STORAGE_KEY, '1')
  }

  return true
}
