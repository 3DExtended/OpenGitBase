const STORAGE_KEY = 'ogb-site-gate-unlocked'

// Cosmetic staging-only gate for local/dev previews. Not a security boundary.
// Production images set NUXT_PUBLIC_SITE_GATE_ENABLED=false and omit the password from the bundle.
const DEV_SITE_GATE_PASSWORD = import.meta.dev ? 'OpenGitBase' : ''

export function isSiteGateUnlocked(): boolean {
  if (!import.meta.client) {
    return false
  }

  return localStorage.getItem(STORAGE_KEY) === '1'
}

export function unlockSiteGate(password: string): boolean {
  if (!import.meta.dev || !DEV_SITE_GATE_PASSWORD) {
    return false
  }

  if (password !== DEV_SITE_GATE_PASSWORD) {
    return false
  }

  if (import.meta.client) {
    localStorage.setItem(STORAGE_KEY, '1')
  }

  return true
}
