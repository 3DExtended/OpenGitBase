import { defineStore } from 'pinia'
import type { AccountMe } from '~/utils/api'

export const useAuthStore = defineStore('auth', () => {
  const user = ref<AccountMe | null>(null)
  const loading = ref(false)
  const initialized = ref(false)

  const isAuthenticated = computed(() => user.value !== null)
  const isEmailVerified = computed(() => user.value?.emailVerified ?? false)

  async function fetchMe() {
    loading.value = true
    try {
      const api = useApi()
      const result = await api.account.me()
      if (result.status === 401 || result.status === 403) {
        user.value = null
        return null
      }
      if (result.data) {
        user.value = result.data
        return result.data
      }
      user.value = null
      return null
    }
    finally {
      loading.value = false
      initialized.value = true
    }
  }

  async function login(username: string, password: string) {
    const api = useApi()
    const result = await api.auth.login({ username, password })
    if (result.error) {
      return result
    }
    await fetchMe()
    return result
  }

  async function register(username: string, email: string, password: string) {
    const api = useApi()
    const result = await api.auth.register({ username, email, password })
    if (result.error) {
      return result
    }
    await fetchMe()
    return result
  }

  async function signOut() {
    const api = useApi()
    await api.auth.signOut()
    user.value = null
  }

  function setEmailVerified(verified: boolean) {
    if (user.value) {
      user.value = { ...user.value, emailVerified: verified }
    }
  }

  return {
    user,
    loading,
    initialized,
    isAuthenticated,
    isEmailVerified,
    fetchMe,
    login,
    register,
    signOut,
    setEmailVerified,
  }
})

export function useAuth() {
  const store = useAuthStore()
  return store
}
