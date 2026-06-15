export function useEmailVerificationDebug() {
  const auth = useAuth()
  const { t } = useI18n()

  const verifying = ref(false)
  const loadingCode = ref(false)
  const code = ref<string | null>(null)
  const expiresAt = ref<string | null>(null)
  const error = ref<string | null>(null)

  const isAvailable = computed(
    () =>
      auth.isAuthenticated
      && !auth.isEmailVerified
      && auth.user?.debug?.emailVerification === true,
  )

  const verifyUrl = computed(() => {
    if (!code.value || !auth.user?.username) {
      return null
    }
    const params = new URLSearchParams({
      username: auth.user.username,
      token: code.value,
    })
    return `/verify-email?${params.toString()}`
  })

  async function verifyNow() {
    verifying.value = true
    error.value = null
    try {
      const api = useApi()
      const result = await api.account.debugVerifyEmail()
      if (result.error) {
        error.value = result.error
        return
      }
      auth.setEmailVerified(true)
      code.value = null
      expiresAt.value = null
    }
    finally {
      verifying.value = false
    }
  }

  async function showCode() {
    loadingCode.value = true
    error.value = null
    try {
      const api = useApi()
      const result = await api.account.debugVerificationCode()
      if (result.error || !result.data) {
        error.value = result.error ?? t('verification.debug.codeError')
        return
      }
      code.value = result.data.code
      expiresAt.value = result.data.expiresAt
    }
    finally {
      loadingCode.value = false
    }
  }

  async function copyCode() {
    if (!code.value) {
      return
    }
    await navigator.clipboard.writeText(code.value)
  }

  return reactive({
    isAvailable,
    verifying,
    loadingCode,
    code,
    expiresAt,
    error,
    verifyUrl,
    verifyNow,
    showCode,
    copyCode,
  })
}
