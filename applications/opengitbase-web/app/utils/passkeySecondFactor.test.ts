import { describe, expect, it, vi } from 'vitest'
import type { ApiResult } from './api'
import type { PasskeySecondFactorApi } from './passkeySecondFactor'
import { passkeySecondFactorAnswer } from './passkeySecondFactor'

const options: PublicKeyCredentialRequestOptionsJSON = {
  challenge: 'AQID',
  rpId: 'localhost',
  allowCredentials: [{ type: 'public-key', id: 'BAU' }],
  userVerification: 'preferred',
}

const assertion = { id: 'BAU', type: 'public-key', response: {} }

function ok<T>(data: T): ApiResult<T> {
  return { data, error: null, status: 200 }
}

function failure<T>(error: string | null, status: number): ApiResult<T> {
  return { data: null, error, status }
}

function fakeApi(result: ApiResult<PublicKeyCredentialRequestOptionsJSON> = ok(options)): PasskeySecondFactorApi {
  return { twoFactorPasskeyOptions: vi.fn(async () => result) }
}

describe('passkeySecondFactorAnswer', () => {
  it('asks for options bound to the challenge and answers with the assertion', async () => {
    const api = fakeApi()
    const getAssertion = vi.fn(async () => assertion)

    const outcome = await passkeySecondFactorAnswer(api, 'challenge-1', getAssertion)

    expect(api.twoFactorPasskeyOptions).toHaveBeenCalledWith({ challengeToken: 'challenge-1' })
    expect(getAssertion).toHaveBeenCalledWith(options)
    expect(outcome).toEqual({ answer: { passkeyAssertion: assertion } })
  })

  it('reports an expired sign-in so the page can start over', async () => {
    const getAssertion = vi.fn()

    const outcome = await passkeySecondFactorAnswer(fakeApi(failure('challenge_expired', 401)), 'old', getAssertion)

    expect(outcome).toEqual({ errorKey: 'auth.twoFactor.errors.challengeExpired', expired: true })
    expect(getAssertion).not.toHaveBeenCalled()
  })

  it('explains an account without passkeys', async () => {
    const outcome = await passkeySecondFactorAnswer(fakeApi(failure('no_passkeys', 400)), 'challenge-1', vi.fn())

    expect(outcome).toEqual({ errorKey: 'settings.security.passkeys.errors.noPasskeys' })
  })

  it('treats a closed prompt as no error', async () => {
    const getAssertion = vi.fn(async () => {
      throw new DOMException('closed', 'NotAllowedError')
    })

    const outcome = await passkeySecondFactorAnswer(fakeApi(), 'challenge-1', getAssertion)

    expect(outcome).toEqual({ errorKey: null })
  })

  it('reports a prompt that failed for another reason', async () => {
    const getAssertion = vi.fn(async () => {
      throw new DOMException('broken', 'SecurityError')
    })

    const outcome = await passkeySecondFactorAnswer(fakeApi(), 'challenge-1', getAssertion)

    expect(outcome).toEqual({ errorKey: 'settings.security.passkeys.errors.generic' })
  })
})
