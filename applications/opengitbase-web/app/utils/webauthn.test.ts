import { afterEach, describe, expect, it, vi } from 'vitest'
import { createApi } from './api'
import {
  base64UrlToBuffer,
  bufferToBase64Url,
  credentialToJSON,
  creationOptionsFromJSON,
  passkeyApiErrorKey,
  passkeyErrorKey,
  requestOptionsFromJSON,
} from './webauthn'

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

function bytes(buffer: ArrayBuffer | BufferSource | undefined): number[] {
  return Array.from(new Uint8Array(buffer as ArrayBuffer))
}

const creationOptions: PublicKeyCredentialCreationOptionsJSON = {
  rp: { id: 'localhost', name: 'OpenGitBase' },
  user: { id: 'AQID', name: 'alice', displayName: 'alice' },
  challenge: '-_8',
  pubKeyCredParams: [{ type: 'public-key', alg: -7 }],
  authenticatorSelection: { residentKey: 'required', userVerification: 'required' },
  attestation: 'none',
  excludeCredentials: [{ type: 'public-key', id: 'BAU', transports: ['internal'] }],
}

describe('webauthn helpers', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('converts base64url without padding both ways', () => {
    expect(bytes(base64UrlToBuffer('-_8'))).toEqual([0xFB, 0xFF])
    expect(bytes(base64UrlToBuffer('AQID'))).toEqual([1, 2, 3])
    expect(bufferToBase64Url(new Uint8Array([0xFB, 0xFF]).buffer)).toBe('-_8')
    expect(bufferToBase64Url(new Uint8Array([1, 2, 3, 4]))).toBe('AQIDBA')
  })

  it('decodes challenge, user id and excluded ids when the browser has no JSON parser', () => {
    const options = creationOptionsFromJSON(creationOptions)

    expect(bytes(options.challenge)).toEqual([0xFB, 0xFF])
    expect(bytes(options.user.id)).toEqual([1, 2, 3])
    expect(bytes(options.excludeCredentials?.[0]?.id)).toEqual([4, 5])
    expect(options.excludeCredentials?.[0]?.transports).toEqual(['internal'])
    expect(options.authenticatorSelection?.userVerification).toBe('required')
  })

  it('decodes challenge and allowed ids of request options', () => {
    const options = requestOptionsFromJSON({
      challenge: 'AQID',
      rpId: 'localhost',
      allowCredentials: [{ type: 'public-key', id: 'BAU' }],
      userVerification: 'required',
    })

    expect(bytes(options.challenge)).toEqual([1, 2, 3])
    expect(bytes(options.allowCredentials?.[0]?.id)).toEqual([4, 5])
    expect(options.rpId).toBe('localhost')
  })

  it('prefers the browser\'s own JSON parser', () => {
    const parsed = { challenge: new ArrayBuffer(1) }
    const parseCreationOptionsFromJSON = vi.fn(() => parsed)
    vi.stubGlobal('PublicKeyCredential', { parseCreationOptionsFromJSON })

    expect(creationOptionsFromJSON(creationOptions)).toBe(parsed)
    expect(parseCreationOptionsFromJSON).toHaveBeenCalledWith(creationOptions)
  })

  it('serialises an attestation like toJSON() when the browser lacks it', () => {
    const credential = {
      id: 'AQID',
      rawId: new Uint8Array([1, 2, 3]).buffer,
      type: 'public-key',
      authenticatorAttachment: 'platform',
      getClientExtensionResults: () => ({}),
      response: {
        clientDataJSON: new Uint8Array([4]).buffer,
        attestationObject: new Uint8Array([5]).buffer,
        getTransports: () => ['internal', 'hybrid'],
      },
    } as unknown as PublicKeyCredential

    expect(credentialToJSON(credential)).toEqual({
      id: 'AQID',
      rawId: 'AQID',
      type: 'public-key',
      authenticatorAttachment: 'platform',
      clientExtensionResults: {},
      response: { clientDataJSON: 'BA', attestationObject: 'BQ', transports: ['internal', 'hybrid'] },
    })
  })

  it('serialises an assertion like toJSON() when the browser lacks it', () => {
    const credential = {
      id: 'AQID',
      rawId: new Uint8Array([1, 2, 3]).buffer,
      type: 'public-key',
      authenticatorAttachment: null,
      getClientExtensionResults: () => ({}),
      response: {
        clientDataJSON: new Uint8Array([4]).buffer,
        authenticatorData: new Uint8Array([5]).buffer,
        signature: new Uint8Array([6]).buffer,
        userHandle: new Uint8Array([7]).buffer,
      },
    } as unknown as PublicKeyCredential

    expect(credentialToJSON(credential)).toEqual({
      id: 'AQID',
      rawId: 'AQID',
      type: 'public-key',
      clientExtensionResults: {},
      response: { clientDataJSON: 'BA', authenticatorData: 'BQ', signature: 'Bg', userHandle: 'Bw' },
    })
  })

  it('uses toJSON() when the browser has it', () => {
    const json = { id: 'x' }
    const credential = { toJSON: () => json } as unknown as PublicKeyCredential

    expect(credentialToJSON(credential)).toBe(json)
  })

  it('tells a cancelled prompt apart from a passkey that is already registered', () => {
    expect(passkeyErrorKey(new DOMException('cancelled', 'NotAllowedError'))).toBe('settings.security.passkeys.errors.cancelled')
    expect(passkeyErrorKey(new DOMException('exists', 'InvalidStateError'))).toBe('settings.security.passkeys.errors.alreadyRegistered')
    expect(passkeyErrorKey(new Error('boom'))).toBe('settings.security.passkeys.errors.generic')
  })

  it('maps API errors of a passkey ceremony to passkey messages', () => {
    expect(passkeyApiErrorKey('challenge_expired', 400)).toBe('settings.security.passkeys.errors.expired')
    expect(passkeyApiErrorKey('passkey_rejected', 401)).toBe('settings.security.passkeys.errors.rejected')
    expect(passkeyApiErrorKey('passkey_already_registered', 409)).toBe('settings.security.passkeys.errors.alreadyRegistered')
    expect(passkeyApiErrorKey('invalid_name', 400)).toBe('settings.security.passkeys.errors.invalidName')
    expect(passkeyApiErrorKey(null, 500)).toBe('auth.twoFactor.errors.generic')
  })
})

describe('passkey API', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('lists, starts, registers, renames and deletes passkeys at their endpoints', async () => {
    const passkey = { id: 'p1', name: 'Laptop', createdAt: '2026-10-18T10:00:00Z', lastUsedAt: null, isBackedUp: true }
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(jsonResponse([passkey]))
      .mockResolvedValueOnce(jsonResponse(creationOptions))
      .mockResolvedValueOnce(jsonResponse(passkey, 201))
      .mockResolvedValueOnce(jsonResponse({ ...passkey, name: 'Phone' }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
    vi.stubGlobal('fetch', fetchMock)
    const api = createApi('/api')

    const list = await api.account.passkeys()
    const options = await api.account.passkeyRegistrationOptions()
    await api.account.registerPasskey({ name: 'Laptop', attestationResponse: { id: 'AQID' } })
    const renamed = await api.account.renamePasskey('p1', { name: 'Phone' })
    await api.account.deletePasskey('p1')

    expect(fetchMock.mock.calls.map(call => [call[0], call[1]?.method ?? 'GET'])).toEqual([
      ['/api/account/passkeys', 'GET'],
      ['/api/account/passkeys/registration-options', 'POST'],
      ['/api/account/passkeys', 'POST'],
      ['/api/account/passkeys/p1', 'PATCH'],
      ['/api/account/passkeys/p1', 'DELETE'],
    ])
    expect(list.data).toEqual([passkey])
    expect(options.data?.challenge).toBe('-_8')
    expect(JSON.parse(fetchMock.mock.calls[2]?.[1]?.body)).toEqual({ name: 'Laptop', attestationResponse: { id: 'AQID' } })
    expect(renamed.data?.name).toBe('Phone')
  })

  it('re-authenticates with a passkey assertion instead of the password', async () => {
    const fetchMock = vi.fn()
      .mockResolvedValueOnce(jsonResponse({ challenge: 'AQID', rpId: 'localhost' }))
      .mockResolvedValueOnce(new Response(null, { status: 204 }))
    vi.stubGlobal('fetch', fetchMock)
    const api = createApi('/api')

    const options = await api.account.passkeyReauthenticationOptions()
    await api.account.reauthenticate({ passkeyAssertion: { id: 'AQID' } })

    expect(fetchMock.mock.calls[0]?.[0]).toBe('/api/account/reauthenticate/passkey-options')
    expect(options.data?.rpId).toBe('localhost')
    expect(JSON.parse(fetchMock.mock.calls[1]?.[1]?.body)).toEqual({ passkeyAssertion: { id: 'AQID' } })
  })
})
