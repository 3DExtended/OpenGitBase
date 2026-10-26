import { createPasskeySignIn } from '~/utils/passkeySignIn'

/**
 * Passkey sign-in for a sign-in form: `available` turns true once the browser and the API both
 * support passkeys, and autofill in the username field starts then. Call `pause()` before the
 * password form signs in and `resume()` when that fails.
 */
export function usePasskeySignIn(
  onSignedIn: (token: string) => void | Promise<void>,
  enabled: () => boolean = () => true,
) {
  const api = useApi()
  const available = ref(false)
  const busy = ref(false)
  const errorKey = ref<string | null>(null)

  const flow = createPasskeySignIn(
    {
      passkeySignInOptions: () => api.auth.passkeySignInOptions(),
      signInWithPasskey: body => api.auth.signInWithPasskey(body),
    },
    {
      onSignedIn,
      onError: (key) => {
        errorKey.value = key
      },
      onBusy: (value) => {
        busy.value = value
      },
    },
  )

  onMounted(async () => {
    if (enabled()) {
      available.value = await flow.prepare()
    }
  })

  onBeforeUnmount(() => {
    flow.dispose()
  })

  return {
    available,
    busy,
    errorKey,
    signIn: () => flow.signInWithButton(),
    pause: () => flow.abort(),
    resume: () => flow.resume(),
  }
}
