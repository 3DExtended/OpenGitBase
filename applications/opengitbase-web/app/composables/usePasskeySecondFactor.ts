import { passkeySecondFactorAnswer } from '~/utils/passkeySecondFactor'
import { webAuthnSupported } from '~/utils/webauthn'

/**
 * The second sign-in step with a passkey: `supported` turns true once the page knows the browser
 * has WebAuthn, and `answer` runs the prompt for an open challenge.
 */
export function usePasskeySecondFactor() {
  const api = useApi()
  const supported = ref(false)

  onMounted(() => {
    supported.value = webAuthnSupported()
  })

  return {
    supported,
    answer: (challengeToken: string) =>
      passkeySecondFactorAnswer({ twoFactorPasskeyOptions: body => api.auth.twoFactorPasskeyOptions(body) }, challengeToken),
  }
}
