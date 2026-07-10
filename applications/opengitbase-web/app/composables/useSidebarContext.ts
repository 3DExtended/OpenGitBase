import { isReservedSlug } from '~/utils/slug-validation'

export type SidebarContext = 'guest' | 'global' | 'owner' | 'repo' | 'settings' | 'admin'

type PathContext = 'owner' | 'repo'

function resolveOwnerOrRepoContext(parts: string[]): PathContext | null {
  if (parts.length < 1 || isReservedSlug(parts[0]!)) {
    return null
  }
  if (parts.length === 1) {
    return 'owner'
  }
  if (parts.length === 2 && parts[1] === 'members') {
    return 'owner'
  }
  if (parts.length >= 2) {
    return 'repo'
  }
  return null
}

export function useSidebarContext() {
  const route = useRoute()
  const auth = useAuth()

  const pathSegments = computed(() => route.path.split('/').filter(Boolean))

  const context = computed<SidebarContext>(() => {
    const parts = pathSegments.value

    if (auth.isAuthenticated) {
      if (parts[0] === 'settings') {
        return 'settings'
      }
      if (parts[0] === 'admin') {
        return 'admin'
      }
      const pathContext = resolveOwnerOrRepoContext(parts)
      if (pathContext === 'owner') {
        return 'owner'
      }
      if (pathContext === 'repo') {
        return 'repo'
      }
      return 'global'
    }

    if (resolveOwnerOrRepoContext(parts) === 'repo') {
      return 'repo'
    }

    return 'guest'
  })

  const ownerSlug = computed(() => {
    if (context.value !== 'owner' && context.value !== 'repo') {
      return null
    }
    return pathSegments.value[0] ?? null
  })

  const repoSlug = computed(() => {
    if (context.value !== 'repo') {
      return null
    }
    return pathSegments.value[1] ?? null
  })

  return {
    context,
    ownerSlug,
    repoSlug,
  }
}

export function isRepoCodeRoute(path: string, owner: string, repo: string): boolean {
  const base = `/${owner}/${repo}`
  if (path === base) {
    return true
  }
  return path.startsWith(`${base}/tree/`) || path.startsWith(`${base}/blob/`)
}

export function matchesSidebarRoute(path: string, to: string, exact = false): boolean {
  if (exact) {
    return path === to
  }
  return path === to || path.startsWith(`${to}/`)
}
