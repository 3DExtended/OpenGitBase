import type { ApiResult } from './api'
import type { PasskeyCredentialJSON } from './webauthn'
import { secondFactorErrorKey } from './twoFactor'
import { getPasskeyAssertion, passkeyErrorKey } from './webauthn'

/** The options call, so the flow runs against the real API or a stand-in. */
export interface PasskeySecondFactorApi {
  twoFactorPasskeyOptions: (body: { challengeToken: string }) => Promise<ApiResult<PublicKeyCredentialRequestOptionsJSON>>
}

export type PasskeySecondFactorOutcome =
  | { answer: { passkeyAssertion: PasskeyCredentialJSON } }
  /** A translation key to show, or null when the prompt was only closed; `expired` starts the sign-in over. */
  | { errorKey: string | null, expired?: true }

/**
 * Answers the open second sign-in step with one of the user's passkeys: the API hands out request
 * options for the challenge, then the browser signs them. The answer goes to `completeTwoFactor`
 * like a code does.
 */
export async function passkeySecondFactorAnswer(
  api: PasskeySecondFactorApi,
  challengeToken: string,
  getAssertion: (options: PublicKeyCredentialRequestOptionsJSON) => Promise<PasskeyCredentialJSON> = getPasskeyAssertion,
): Promise<PasskeySecondFactorOutcome> {
  const options = await api.twoFactorPasskeyOptions({ challengeToken })
  if (options.error || !options.data) {
    return options.error === 'challenge_expired'
      ? { errorKey: 'auth.twoFactor.errors.challengeExpired', expired: true }
      : { errorKey: secondFactorErrorKey(options.error, options.status) }
  }
  try {
    return { answer: { passkeyAssertion: await getAssertion(options.data) } }
  }
  catch (error) {
    const key = passkeyErrorKey(error)
    return { errorKey: key === 'settings.security.passkeys.errors.cancelled' ? null : key }
  }
}
