import { createApi } from '~/utils/api'

let cachedClient: ReturnType<typeof createApi> | null = null
let cachedBaseUrl: string | null = null

export function useApi() {
  const config = useRuntimeConfig()
  const baseUrl = config.public.apiBase as string

  if (!cachedClient || cachedBaseUrl !== baseUrl) {
    cachedClient = createApi(baseUrl)
    cachedBaseUrl = baseUrl
  }

  return cachedClient
}
