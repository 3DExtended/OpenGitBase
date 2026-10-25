import { afterEach, describe, expect, it, vi } from 'vitest'
import { createApi } from './api'
import {
  formatSetupKey,
  isTwoFactorChallenge,
  normalizeAuthenticatorCode,
  recoveryCodesFileContent,
  recoveryCodesRunningLow,
  secondFactorErrorKey,
} from './twoFactor'

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

function textResponse(body: string, status = 200): Response {
  return new Response(body, { status, headers: { 'Content-Type': 'text/plain; charset=utf-8' } })
}

describe('two-factor helpers', () => {
  it('recognises a sign-in challenge but not a session token', () => {
    expect(isTwoFactorChallenge({ twoFactorRequired: true, challengeToken: 'c', methods: ['totp'] })).toBe(true)
    expect(isTwoFactorChallenge('eyJhbGciOiJIUzUxMiJ9.e30.sig')).toBe(false)
    expect(isTwoFactorChallenge(null)).toBe(false)
    expect(isTwoFactorChallenge({ twoFactorRequired: false })).toBe(false)
  })

  it('keeps only the digits of a pasted code', () => {
    expect(normalizeAuthenticatorCode(' 123 456 ')).toBe('123456')
    expect(normalizeAuthenticatorCode('123-456')).toBe('123456')
  })

  it('groups the setup key in blocks of four for typing', () => {
    expect(formatSetupKey('JBSWY3DPEHPK3PXPJBSW')).toBe('JBSW Y3DP EHPK 3PXP JBSW')
  })

  it('maps API errors to translation keys', () => {
    expect(secondFactorErrorKey('invalid_code', 401)).toBe('auth.twoFactor.errors.invalidCode')
    expect(secondFactorErrorKey('challenge_expired', 401)).toBe('auth.twoFactor.errors.challengeExpired')
    expect(secondFactorErrorKey('locked_out', 429)).toBe('auth.twoFactor.errors.lockedOut')
    // The rate limiter answers 429 without a body.
    expect(secondFactorErrorKey('Too Many Requests', 429)).toBe('auth.twoFactor.errors.lockedOut')
    expect(secondFactorErrorKey('invalid_credentials', 401)).toBe('auth.twoFactor.errors.invalidCredentials')
    expect(secondFactorErrorKey('no_pending_setup', 400)).toBe('auth.twoFactor.errors.setupExpired')
    expect(secondFactorErrorKey('two_factor_not_enabled', 400)).toBe('auth.twoFactor.errors.notEnabled')
    expect(secondFactorErrorKey('boom', 500)).toBe('auth.twoFactor.errors.generic')
  })

  it('says "recovery code" when a recovery code was wrong', () => {
    expect(secondFactorErrorKey('invalid_code', 401, 'recovery')).toBe('auth.twoFactor.errors.invalidRecoveryCode')
    expect(secondFactorErrorKey('locked_out', 429, 'recovery')).toBe('auth.twoFactor.errors.lockedOut')
  })
})

describe('recovery codes', () => {
  it('writes one code per line under a header naming the account and site', () => {
    const content = recoveryCodesFileContent(['abcde-fghij', 'klmno-pqrst'], {
      account: 'alice',
      site: 'opengitbase.com',
      createdAt: new Date('2026-10-18T12:00:00Z'),
    })

    expect(content.split('\n')).toEqual([
      'OpenGitBase recovery codes for alice (opengitbase.com)',
      'Created 2026-10-18. Each code works once.',
      '',
      'abcde-fghij',
      'klmno-pqrst',
      '',
    ])
  })

  it('warns from two remaining codes down', () => {
    expect(recoveryCodesRunningLow(3)).toBe(false)
    expect(recoveryCodesRunningLow(2)).toBe(true)
    expect(recoveryCodesRunningLow(0)).toBe(true)
  })
})

describe('two-factor API client', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('returns the challenge when sign-in needs a second step', async () => {
    const challenge = { twoFactorRequired: true, challengeToken: 'challenge-1', methods: ['totp'] }
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(jsonResponse(challenge)))

    const result = await createApi('/api').auth.login({ username: 'alice', password: 'pw' })

    expect(result.data).toEqual(challenge)
  })

  it('still returns the plain session token without 2FA', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(textResponse('token-1')))

    const result = await createApi('/api').auth.login({ username: 'alice', password: 'pw' })

    expect(result.data).toBe('token-1')
  })

  it('answers the challenge with the code', async () => {
    const fetchMock = vi.fn().mockResolvedValue(textResponse('token-2'))
    vi.stubGlobal('fetch', fetchMock)

    const result = await createApi('/api').auth.completeTwoFactor({ challengeToken: 'challenge-1', code: '123456' })

    expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/signin/two-factor')
    expect(JSON.parse(fetchMock.mock.calls[0]?.[1]?.body as string)).toEqual({
      challengeToken: 'challenge-1',
      code: '123456',
    })
    expect(result.data).toBe('token-2')
  })

  it('answers the challenge with a recovery code instead', async () => {
    const fetchMock = vi.fn().mockResolvedValue(textResponse('token-3'))
    vi.stubGlobal('fetch', fetchMock)

    await createApi('/api').auth.completeTwoFactor({ challengeToken: 'challenge-1', recoveryCode: 'abcde-fghij' })

    expect(JSON.parse(fetchMock.mock.calls[0]?.[1]?.body as string)).toEqual({
      challengeToken: 'challenge-1',
      recoveryCode: 'abcde-fghij',
    })
  })

  it('surfaces the error code of a wrong code', async () => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(jsonResponse({ error: 'invalid_code' }, 401)))

    const result = await createApi('/api').auth.completeTwoFactor({ challengeToken: 'c', code: '000000' })

    expect(result).toMatchObject({ data: null, error: 'invalid_code', status: 401 })
  })

  it('reads the security status and twoFactorEnabled on /account/me', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(jsonResponse({
        twoFactorAvailable: true,
        twoFactorEnabled: true,
        totpConfigured: true,
        hasPassword: true,
      }))
      .mockResolvedValueOnce(jsonResponse({
        userId: 'u1',
        username: 'alice',
        emailVerified: true,
        isAdmin: false,
        twoFactorEnabled: true,
      }))
    vi.stubGlobal('fetch', fetchMock)
    const api = createApi('/api')

    const security = await api.account.security()
    const me = await api.account.me()

    expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/account/security')
    expect(security.data?.totpConfigured).toBe(true)
    expect(me.data?.twoFactorEnabled).toBe(true)
  })

  it('posts setup, confirm, re-authentication and disable to their endpoints', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
      .mockResolvedValueOnce(jsonResponse({ otpauthUri: 'otpauth://totp/x', secret: 'ABC' }))
      .mockResolvedValueOnce(jsonResponse({ twoFactorEnabled: true }))
      .mockResolvedValueOnce(jsonResponse({ twoFactorEnabled: false }))
    vi.stubGlobal('fetch', fetchMock)
    const api = createApi('/api')

    await api.account.reauthenticate({ password: 'pw', code: '123456' })
    const setup = await api.account.startTotpSetup()
    await api.account.confirmTotpSetup({ code: '654321' })
    await api.account.disableTwoFactor()

    expect(fetchMock.mock.calls.map(call => call[0])).toEqual([
      '/api/account/reauthenticate',
      '/api/account/two-factor/totp/setup',
      '/api/account/two-factor/totp/confirm',
      '/api/account/two-factor/disable',
    ])
    expect(setup.data).toEqual({ otpauthUri: 'otpauth://totp/x', secret: 'ABC' })
  })

  it('returns the first recovery codes on confirm and a new set on regenerate', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(jsonResponse({ twoFactorEnabled: true, recoveryCodes: ['abcde-fghij'] }))
      .mockResolvedValueOnce(jsonResponse({ recoveryCodes: ['klmno-pqrst'] }))
    vi.stubGlobal('fetch', fetchMock)
    const api = createApi('/api')

    const confirmed = await api.account.confirmTotpSetup({ code: '654321' })
    const regenerated = await api.account.regenerateRecoveryCodes()

    expect(confirmed.data?.recoveryCodes).toEqual(['abcde-fghij'])
    expect(fetchMock.mock.calls[1]?.[0]).toBe('/api/account/two-factor/recovery-codes')
    expect(fetchMock.mock.calls[1]?.[1]?.method).toBe('POST')
    expect(regenerated.data?.recoveryCodes).toEqual(['klmno-pqrst'])
  })
})
