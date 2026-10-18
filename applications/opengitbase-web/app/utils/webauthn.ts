import { secondFactorErrorKey } from './twoFactor'

/**
 * Passkeys in the browser. The API sends WebAuthn options as JSON and takes the answer the way
 * `PublicKeyCredential.toJSON()` writes it; these helpers bridge to `navigator.credentials` and fall
 * back to plain base64url conversion in browsers without the JSON methods.
 */

export interface Passkey {
  id: string
  name: string
  createdAt: string
  lastUsedAt: string | null
  /** Synced, for example by iCloud Keychain or Google Password Manager. */
  isBackedUp: boolean
}

/** The credential as `PublicKeyCredential.toJSON()` returns it. */
export type PasskeyCredentialJSON = Record<string, unknown>

/** The longest name a passkey may have, as the API enforces. */
export const PASSKEY_NAME_MAX_LENGTH = 64

interface JsonParsers {
  parseCreationOptionsFromJSON?: (options: PublicKeyCredentialCreationOptionsJSON) => PublicKeyCredentialCreationOptions
  parseRequestOptionsFromJSON?: (options: PublicKeyCredentialRequestOptionsJSON) => PublicKeyCredentialRequestOptions
}

function nativeParsers(): JsonParsers | undefined {
  return typeof PublicKeyCredential === 'undefined' ? undefined : (PublicKeyCredential as unknown as JsonParsers)
}

/** Old browsers and some embedded web views have no WebAuthn; the passkey UI stays hidden there. */
export function webAuthnSupported(): boolean {
  return typeof window !== 'undefined'
    && typeof window.PublicKeyCredential === 'function'
    && typeof navigator.credentials?.create === 'function'
}

export function base64UrlToBuffer(value: string): ArrayBuffer {
  const base64 = value.replace(/-/g, '+').replace(/_/g, '/')
  const binary = atob(base64.padEnd(base64.length + ((4 - (base64.length % 4)) % 4), '='))
  const result = new Uint8Array(binary.length)
  for (let index = 0; index < binary.length; index++) {
    result[index] = binary.charCodeAt(index)
  }
  return result.buffer
}

export function bufferToBase64Url(buffer: ArrayBuffer | ArrayBufferView): string {
  const view = buffer instanceof ArrayBuffer
    ? new Uint8Array(buffer)
    : new Uint8Array(buffer.buffer, buffer.byteOffset, buffer.byteLength)
  let binary = ''
  for (const byte of view) {
    binary += String.fromCharCode(byte)
  }
  return btoa(binary).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '')
}

function descriptors(list: PublicKeyCredentialDescriptorJSON[] | undefined): PublicKeyCredentialDescriptor[] | undefined {
  return list?.map(descriptor => ({
    ...descriptor,
    type: descriptor.type as PublicKeyCredentialType,
    id: base64UrlToBuffer(descriptor.id),
    transports: descriptor.transports as AuthenticatorTransport[] | undefined,
  }))
}

export function creationOptionsFromJSON(json: PublicKeyCredentialCreationOptionsJSON): PublicKeyCredentialCreationOptions {
  const native = nativeParsers()?.parseCreationOptionsFromJSON
  if (native) {
    return native.call(PublicKeyCredential, json)
  }
  return {
    ...json,
    challenge: base64UrlToBuffer(json.challenge),
    user: { ...json.user, id: base64UrlToBuffer(json.user.id) },
    excludeCredentials: descriptors(json.excludeCredentials),
  } as PublicKeyCredentialCreationOptions
}

export function requestOptionsFromJSON(json: PublicKeyCredentialRequestOptionsJSON): PublicKeyCredentialRequestOptions {
  const native = nativeParsers()?.parseRequestOptionsFromJSON
  if (native) {
    return native.call(PublicKeyCredential, json)
  }
  return {
    ...json,
    challenge: base64UrlToBuffer(json.challenge),
    allowCredentials: descriptors(json.allowCredentials),
  } as PublicKeyCredentialRequestOptions
}

export function credentialToJSON(credential: PublicKeyCredential): PasskeyCredentialJSON {
  if (typeof credential.toJSON === 'function') {
    return credential.toJSON()
  }

  const base = {
    id: credential.id,
    rawId: bufferToBase64Url(credential.rawId),
    type: credential.type,
    ...(credential.authenticatorAttachment ? { authenticatorAttachment: credential.authenticatorAttachment } : {}),
    clientExtensionResults: credential.getClientExtensionResults(),
  }
  if ('attestationObject' in credential.response) {
    const attestation = credential.response as AuthenticatorAttestationResponse
    return {
      ...base,
      response: {
        clientDataJSON: bufferToBase64Url(attestation.clientDataJSON),
        attestationObject: bufferToBase64Url(attestation.attestationObject),
        transports: attestation.getTransports?.() ?? [],
      },
    }
  }

  const assertion = credential.response as AuthenticatorAssertionResponse
  return {
    ...base,
    response: {
      clientDataJSON: bufferToBase64Url(assertion.clientDataJSON),
      authenticatorData: bufferToBase64Url(assertion.authenticatorData),
      signature: bufferToBase64Url(assertion.signature),
      ...(assertion.userHandle ? { userHandle: bufferToBase64Url(assertion.userHandle) } : {}),
    },
  }
}

/** Asks the browser for a new passkey; throws a `DOMException` when the prompt is cancelled or fails. */
export async function createPasskey(options: PublicKeyCredentialCreationOptionsJSON): Promise<PasskeyCredentialJSON> {
  const credential = await navigator.credentials.create({ publicKey: creationOptionsFromJSON(options) })
  if (!credential) {
    throw new DOMException('No passkey was created.', 'NotAllowedError')
  }
  return credentialToJSON(credential as PublicKeyCredential)
}

/** Asks the browser to sign the challenge with one of the allowed passkeys. */
export async function getPasskeyAssertion(options: PublicKeyCredentialRequestOptionsJSON): Promise<PasskeyCredentialJSON> {
  const credential = await navigator.credentials.get({ publicKey: requestOptionsFromJSON(options) })
  if (!credential) {
    throw new DOMException('No passkey was used.', 'NotAllowedError')
  }
  return credentialToJSON(credential as PublicKeyCredential)
}

/**
 * The translation key for a failed browser prompt. Browsers report a cancelled or timed-out prompt
 * as `NotAllowedError` and an authenticator that already holds one of the excluded passkeys as
 * `InvalidStateError`.
 */
export function passkeyErrorKey(error: unknown): string {
  const name = error instanceof DOMException || error instanceof Error ? error.name : ''
  if (name === 'NotAllowedError' || name === 'AbortError') {
    return 'settings.security.passkeys.errors.cancelled'
  }
  if (name === 'InvalidStateError') {
    return 'settings.security.passkeys.errors.alreadyRegistered'
  }
  return 'settings.security.passkeys.errors.generic'
}

/** The translation key for a failed passkey request to the API. */
export function passkeyApiErrorKey(error: string | null, status: number): string {
  if (error === 'challenge_expired') {
    return 'settings.security.passkeys.errors.expired'
  }
  return secondFactorErrorKey(error, status)
}
