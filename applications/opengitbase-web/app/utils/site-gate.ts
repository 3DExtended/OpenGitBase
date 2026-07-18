const STORAGE_KEY = 'ogb-site-gate-unlocked'
const SITE_GATE_PASSWORD = 'OpenGitBase'

// Cosmetic site-wide preview lock. Not a security boundary — unlock is a forgeable
// session cookie and the password ships in the client bundle when the gate is enabled.

function canUseDocumentCookie(): boolean {
  return typeof document !== 'undefined'
}

function readCookie(name: string): string | null {
  if (!canUseDocumentCookie()) {
    return null
  }

  const prefix = `${name}=`
  for (const part of document.cookie.split(';')) {
    const trimmed = part.trim()
    if (trimmed.startsWith(prefix)) {
      return decodeURIComponent(trimmed.slice(prefix.length))
    }
  }

  return null
}

function writeSessionCookie(name: string, value: string): void {
  if (!canUseDocumentCookie()) {
    return
  }

  document.cookie = `${name}=${encodeURIComponent(value)}; Path=/; SameSite=Lax`
}

export function isSiteGateUnlocked(): boolean {
  return readCookie(STORAGE_KEY) === '1'
}

export function unlockSiteGate(password: string): boolean {
  if (password !== SITE_GATE_PASSWORD) {
    return false
  }

  writeSessionCookie(STORAGE_KEY, '1')
  return true
}
