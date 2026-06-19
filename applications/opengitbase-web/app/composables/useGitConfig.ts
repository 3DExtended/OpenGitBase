import type { GitConfig } from '~/utils/api'

let cachedConfig: GitConfig | null = null
let loadPromise: Promise<GitConfig> | null = null

function stripWwwFromOrigin(origin: string): string {
  try {
    const url = new URL(origin)
    if (url.hostname.startsWith('www.')) {
      url.hostname = url.hostname.slice(4)
    }
    return url.origin
  }
  catch {
    return origin
  }
}

export function useGitConfig() {
  const config = useState<GitConfig | null>('git-config', () => cachedConfig)

  async function load(): Promise<GitConfig> {
    if (cachedConfig) {
      config.value = cachedConfig
      return cachedConfig
    }

    if (!loadPromise) {
      loadPromise = (async () => {
        const api = useApi()
        const result = await api.git.getConfig()
        const fallbackBase = stripWwwFromOrigin(
          import.meta.client ? window.location.origin : 'http://localhost:8089',
        )
        const resolved: GitConfig = {
          gitBaseUrl: result.data?.gitBaseUrl || fallbackBase,
          sshEnabled: result.data?.sshEnabled ?? false,
        }
        cachedConfig = resolved
        config.value = resolved
        return resolved
      })()
    }

    return loadPromise
  }

  return {
    config,
    load,
  }
}
