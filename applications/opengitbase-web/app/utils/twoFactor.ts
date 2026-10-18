/** What sign-in answers instead of a session token when the account has 2FA on. */
export interface TwoFactorChallenge {
  twoFactorRequired: true
  challengeToken: string
  methods: string[]
}

export interface AccountSecurity {
  /** Whether 2FA can be turned on on this instance (`TwoFactor:Enabled`). */
  twoFactorAvailable: boolean
  twoFactorEnabled: boolean
  totpConfigured: boolean
  /** False for Google/Apple-only accounts. */
  hasPassword: boolean
}

export interface TotpSetup {
  otpauthUri: string
  secret: string
}

export function isTwoFactorChallenge(value: unknown): value is TwoFactorChallenge {
  return typeof value === 'object'
    && value !== null
    && (value as { twoFactorRequired?: unknown }).twoFactorRequired === true
    && typeof (value as { challengeToken?: unknown }).challengeToken === 'string'
}

/** Authenticator apps show codes as "123 456"; people paste them that way. */
export function normalizeAuthenticatorCode(input: string): string {
  return input.replace(/\D/g, '')
}

export function formatSetupKey(secret: string): string {
  return secret.match(/.{1,4}/g)?.join(' ') ?? secret
}

const errorKeys: Record<string, string> = {
  invalid_code: 'auth.twoFactor.errors.invalidCode',
  challenge_expired: 'auth.twoFactor.errors.challengeExpired',
  locked_out: 'auth.twoFactor.errors.lockedOut',
  invalid_credentials: 'auth.twoFactor.errors.invalidCredentials',
  second_factor_required: 'auth.twoFactor.errors.codeRequired',
  no_pending_setup: 'auth.twoFactor.errors.setupExpired',
  reauthentication_required: 'auth.twoFactor.errors.reauthenticationRequired',
  reauthentication_unavailable: 'auth.twoFactor.errors.reauthenticationUnavailable',
}

/** The translation key for a failed second-factor or re-authentication request. */
export function secondFactorErrorKey(error: string | null, status: number): string {
  if (error && errorKeys[error]) {
    return errorKeys[error]
  }
  if (status === 429) {
    return 'auth.twoFactor.errors.lockedOut'
  }
  return 'auth.twoFactor.errors.generic'
}
