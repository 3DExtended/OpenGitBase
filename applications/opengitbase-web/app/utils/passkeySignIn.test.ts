import { afterEach, describe, expect, it, vi } from 'vitest'
import type { ApiResult } from './api'
import type { PasskeySignInApi } from './passkeySignIn'
import { createPasskeySignIn } from './passkeySignIn'

const options: PublicKeyCredentialRequestOptionsJSON = {
  challenge: 'AQID',
  rpId: 'localhost',
  allowCredentials: [],
  userVerification: 'required',
}

const assertion = { id: 'BAU', type: 'public-key', response: {} }

function ok<T>(data: T): ApiResult<T> {
  return { data, error: null, status: 200 }
}

function failure<T>(error: string, status: number): ApiResult<T> {
  return { data: null, error, status }
}

function fakeApi(overrides: Partial<PasskeySignInApi> = {}): PasskeySignInApi {
  return {
    passkeySignInOptions: vi.fn(async () => ok(options)),
    signInWithPasskey: vi.fn(async () => ok('session-token')),
    ...overrides,
  }
}

interface PendingGet {
  request: CredentialRequestOptions
  resolve: (credential: unknown) => void
  reject: (error: unknown) => void
}

/** A browser with WebAuthn whose `navigator.credentials.get` calls the test answers by hand. */
function stubBrowser(conditionalMediation: boolean) {
  const pending: PendingGet[] = []
  const get = vi.fn((request: CredentialRequestOptions) =>
    new Promise((resolve, reject) => {
      pending.push({ request, resolve, reject })
      request.signal?.addEventListener('abort', () =>
        reject(new DOMException('aborted', 'AbortError')))
    }))
  const FakePublicKeyCredential = Object.assign(function PublicKeyCredential() {}, {
    isConditionalMediationAvailable: async () => conditionalMediation,
    parseRequestOptionsFromJSON: (json: PublicKeyCredentialRequestOptionsJSON) => json,
  })
  vi.stubGlobal('window', { PublicKeyCredential: FakePublicKeyCredential })
  vi.stubGlobal('PublicKeyCredential', FakePublicKeyCredential)
  vi.stubGlobal('navigator', { credentials: { get, create: vi.fn() } })
  return { get, pending }
}

function credential() {
  return { toJSON: () => assertion }
}

/** Lets every pending promise run. */
function settle() {
  return new Promise(resolve => setTimeout(resolve, 0))
}

describe('passkey sign-in', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('stays hidden without WebAuthn and asks the API for nothing', async () => {
    vi.stubGlobal('window', {})
    const api = fakeApi()
    const signIn = createPasskeySignIn(api, { onSignedIn: vi.fn(), onError: vi.fn() })

    expect(await signIn.prepare()).toBe(false)
    expect(api.passkeySignInOptions).not.toHaveBeenCalled()
  })

  it('stays hidden when the API has passkeys switched off', async () => {
    const { get } = stubBrowser(true)
    const api = fakeApi({ passkeySignInOptions: vi.fn(async () => failure<PublicKeyCredentialRequestOptionsJSON>('Not Found', 404)) })
    const signIn = createPasskeySignIn(api, { onSignedIn: vi.fn(), onError: vi.fn() })

    expect(await signIn.prepare()).toBe(false)
    expect(get).not.toHaveBeenCalled()
  })

  it('offers passkeys in the username field and signs in with the one picked there', async () => {
    const { get, pending } = stubBrowser(true)
    const api = fakeApi()
    const onSignedIn = vi.fn()
    const signIn = createPasskeySignIn(api, { onSignedIn, onError: vi.fn() })

    expect(await signIn.prepare()).toBe(true)
    await settle()
    expect(get).toHaveBeenCalledTimes(1)
    expect(pending[0]!.request.mediation).toBe('conditional')
    expect(pending[0]!.request.signal).toBeDefined()

    pending[0]!.resolve(credential())
    await settle()

    expect(api.signInWithPasskey).toHaveBeenCalledWith({ assertion })
    expect(onSignedIn).toHaveBeenCalledWith('session-token')
  })

  it('without conditional mediation only shows the button', async () => {
    const { get } = stubBrowser(false)
    const signIn = createPasskeySignIn(fakeApi(), { onSignedIn: vi.fn(), onError: vi.fn() })

    expect(await signIn.prepare()).toBe(true)
    await settle()

    expect(get).not.toHaveBeenCalled()
  })

  it('the button cancels the pending autofill request and asks with fresh options', async () => {
    const { get, pending } = stubBrowser(true)
    const api = fakeApi()
    const onSignedIn = vi.fn()
    const onError = vi.fn()
    const signIn = createPasskeySignIn(api, { onSignedIn, onError })
    await signIn.prepare()
    await settle()

    const button = signIn.signInWithButton()
    await settle()
    expect(pending[0]!.request.signal?.aborted).toBe(true)
    expect(get).toHaveBeenCalledTimes(2)
    expect(pending[1]!.request.mediation).toBeUndefined()
    expect(api.passkeySignInOptions).toHaveBeenCalledTimes(2)
    pending[1]!.resolve(credential())
    await button

    expect(onSignedIn).toHaveBeenCalledWith('session-token')
    expect(onError).not.toHaveBeenCalledWith(expect.any(String))
  })

  it('submitting the password form cancels the autofill request quietly', async () => {
    const { pending } = stubBrowser(true)
    const onError = vi.fn()
    const signIn = createPasskeySignIn(fakeApi(), { onSignedIn: vi.fn(), onError })
    await signIn.prepare()
    await settle()

    signIn.abort()
    await settle()

    expect(pending[0]!.request.signal?.aborted).toBe(true)
    expect(onError).not.toHaveBeenCalled()
  })

  it('reports a passkey the API rejects and offers autofill again', async () => {
    const { get, pending } = stubBrowser(true)
    const api = fakeApi({ signInWithPasskey: vi.fn(async () => failure<string>('passkey_rejected', 401)) })
    const onSignedIn = vi.fn()
    const onError = vi.fn()
    const signIn = createPasskeySignIn(api, { onSignedIn, onError })
    await signIn.prepare()
    await settle()

    pending[0]!.resolve(credential())
    await settle()
    await settle()

    expect(onSignedIn).not.toHaveBeenCalled()
    expect(onError).toHaveBeenLastCalledWith('auth.signIn.passkey.rejected')
    expect(get).toHaveBeenCalledTimes(2)
    expect(pending[1]!.request.mediation).toBe('conditional')
  })

  it('a cancelled prompt shows no error', async () => {
    const { pending } = stubBrowser(false)
    const onError = vi.fn()
    const signIn = createPasskeySignIn(fakeApi(), { onSignedIn: vi.fn(), onError })
    await signIn.prepare()

    const button = signIn.signInWithButton()
    await settle()
    pending[0]!.reject(new DOMException('cancelled', 'NotAllowedError'))
    await button

    expect(onError).toHaveBeenLastCalledWith(null)
  })

  it('a too many requests answer says so', async () => {
    stubBrowser(false)
    const api = fakeApi({ passkeySignInOptions: vi.fn()
      .mockResolvedValueOnce(ok(options))
      .mockResolvedValueOnce(failure<PublicKeyCredentialRequestOptionsJSON>('Too Many Requests', 429)) })
    const onError = vi.fn()
    const signIn = createPasskeySignIn(api, { onSignedIn: vi.fn(), onError })
    await signIn.prepare()

    await signIn.signInWithButton()

    expect(onError).toHaveBeenLastCalledWith('auth.signIn.passkey.tooManyAttempts')
  })
})
