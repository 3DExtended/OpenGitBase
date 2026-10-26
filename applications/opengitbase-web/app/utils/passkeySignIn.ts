import type { ApiResult } from './api'
import type { PasskeyCredentialJSON } from './webauthn'
import { getPasskeyAssertion, webAuthnSupported } from './webauthn'

/** The two sign-in calls, so the flow runs against the real API or a stand-in. */
export interface PasskeySignInApi {
  passkeySignInOptions: () => Promise<ApiResult<PublicKeyCredentialRequestOptionsJSON>>
  signInWithPasskey: (body: { assertion: PasskeyCredentialJSON }) => Promise<ApiResult<string>>
}

export interface PasskeySignInHandlers {
  onSignedIn: (token: string) => void | Promise<void>
  /** A translation key to show, or null to clear it; a cancelled prompt clears it. */
  onError: (key: string | null) => void
  /** True while the button's prompt or the sign-in request runs. */
  onBusy?: (busy: boolean) => void
}

function isCancelled(error: unknown): boolean {
  return error instanceof DOMException && (error.name === 'NotAllowedError' || error.name === 'AbortError')
}

function failedRequestKey(status: number, fallback: string): string {
  return status === 429 ? 'auth.signIn.passkey.tooManyAttempts' : fallback
}

async function supportsConditionalMediation(): Promise<boolean> {
  const check = (PublicKeyCredential as unknown as { isConditionalMediationAvailable?: () => Promise<boolean> })
    .isConditionalMediationAvailable
  if (typeof check !== 'function') {
    return false
  }
  try {
    return await check.call(PublicKeyCredential)
  }
  catch {
    return false
  }
}

/**
 * Passwordless sign-in. Where the browser supports conditional mediation, the username field offers
 * the passkeys it holds for this site (autofill); the button opens the browser's own prompt instead.
 * Only one WebAuthn request may be pending, so the button and the password form cancel the autofill
 * request first, and a failed attempt offers autofill again.
 */
export function createPasskeySignIn(api: PasskeySignInApi, handlers: PasskeySignInHandlers) {
  let conditional = false
  let autofill: AbortController | null = null
  let finished = false

  async function complete(assertion: PasskeyCredentialJSON) {
    handlers.onBusy?.(true)
    try {
      const result = await api.signInWithPasskey({ assertion })
      if (result.data) {
        finished = true
        handlers.onError(null)
        await handlers.onSignedIn(result.data)
        return
      }
      handlers.onError(failedRequestKey(result.status, 'auth.signIn.passkey.rejected'))
    }
    finally {
      handlers.onBusy?.(false)
    }
    void resume()
  }

  /** Offers the passkeys in the username field until one is picked or the request is cancelled. */
  async function resume(options?: PublicKeyCredentialRequestOptionsJSON) {
    if (!conditional || finished || autofill) {
      return
    }
    const controller = new AbortController()
    autofill = controller
    let assertion: PasskeyCredentialJSON
    try {
      const requestOptions = options ?? (await api.passkeySignInOptions()).data
      if (!requestOptions || controller.signal.aborted) {
        return
      }
      assertion = await getPasskeyAssertion(requestOptions, { mediation: 'conditional', signal: controller.signal })
    }
    catch {
      // Cancelled by the button or the password form, or the browser gave up: nothing to show.
      return
    }
    finally {
      if (autofill === controller) {
        autofill = null
      }
    }
    await complete(assertion)
  }

  /** Shows the button when the browser and the API both support passkeys, and starts autofill. */
  async function prepare(): Promise<boolean> {
    if (!webAuthnSupported()) {
      return false
    }
    const options = await api.passkeySignInOptions()
    if (!options.data) {
      return false
    }
    conditional = await supportsConditionalMediation()
    void resume(options.data)
    return true
  }

  /** Cancels the autofill request, for example before the password form signs in. */
  function abort() {
    const controller = autofill
    autofill = null
    controller?.abort()
  }

  async function askWithPrompt(): Promise<PasskeyCredentialJSON | null> {
    handlers.onBusy?.(true)
    try {
      const options = await api.passkeySignInOptions()
      if (!options.data) {
        handlers.onError(failedRequestKey(options.status, 'auth.signIn.passkey.failed'))
        return null
      }
      return await getPasskeyAssertion(options.data)
    }
    catch (error) {
      handlers.onError(isCancelled(error) ? null : 'auth.signIn.passkey.failed')
      return null
    }
    finally {
      handlers.onBusy?.(false)
    }
  }

  async function signInWithButton() {
    abort()
    handlers.onError(null)
    const assertion = await askWithPrompt()
    if (assertion) {
      await complete(assertion)
    }
    else {
      void resume()
    }
  }

  function dispose() {
    finished = true
    abort()
  }

  return { prepare, signInWithButton, abort, resume: () => resume(), dispose }
}
