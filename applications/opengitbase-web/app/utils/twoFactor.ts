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
  /** Unused recovery codes. */
  recoveryCodesRemaining: number
}

/** How the second step is answered: the authenticator code or one recovery code. */
export type SecondFactorAnswer = { code: string } | { recoveryCode: string }

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

/** The warning shows from this many unused recovery codes down. */
export const RECOVERY_CODES_LOW = 2

export function recoveryCodesRunningLow(remaining: number): boolean {
  return remaining <= RECOVERY_CODES_LOW
}

/** The text file people download to keep their recovery codes. */
export function recoveryCodesFileContent(
  codes: string[],
  context: { account: string, site: string, createdAt: Date },
): string {
  const created = context.createdAt.toISOString().slice(0, 10)
  return [
    `OpenGitBase recovery codes for ${context.account} (${context.site})`,
    `Created ${created}. Each code works once.`,
    '',
    ...codes,
    '',
  ].join('\n')
}

const errorKeys: Record<string, string> = {
  invalid_code: 'auth.twoFactor.errors.invalidCode',
  challenge_expired: 'auth.twoFactor.errors.challengeExpired',
  locked_out: 'auth.twoFactor.errors.lockedOut',
  invalid_credentials: 'auth.twoFactor.errors.invalidCredentials',
  second_factor_required: 'auth.twoFactor.errors.codeRequired',
  no_pending_setup: 'auth.twoFactor.errors.setupExpired',
  two_factor_not_enabled: 'auth.twoFactor.errors.notEnabled',
  reauthentication_required: 'auth.twoFactor.errors.reauthenticationRequired',
  reauthentication_unavailable: 'auth.twoFactor.errors.reauthenticationUnavailable',
}

/** The translation key for a failed second-factor or re-authentication request. */
export function secondFactorErrorKey(
  error: string | null,
  status: number,
  method: 'code' | 'recovery' = 'code',
): string {
  if (error === 'invalid_code' && method === 'recovery') {
    return 'auth.twoFactor.errors.invalidRecoveryCode'
  }
  if (error && errorKeys[error]) {
    return errorKeys[error]
  }
  if (status === 429) {
    return 'auth.twoFactor.errors.lockedOut'
  }
  return 'auth.twoFactor.errors.generic'
}
