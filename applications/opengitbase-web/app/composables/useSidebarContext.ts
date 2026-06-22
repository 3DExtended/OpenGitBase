export type SidebarContext = 'guest' | 'global' | 'owner' | 'repo' | 'settings' | 'admin'

const RESERVED_TOP_LEVEL = new Set([
  'settings',
  'admin',
  'repos',
  'orgs',
  'explore',
  'sign-in',
  'sign-up',
  'sign-out',
  'verify-email',
  'forgot-password',
  'reset-password',
  'gate',
  'invite',
  '__visual__',
])

export function useSidebarContext() {
  const route = useRoute()
  const auth = useAuth()

  const pathSegments = computed(() => route.path.split('/').filter(Boolean))

  const context = computed<SidebarContext>(() => {
    if (!auth.isAuthenticated) {
      return 'guest'
    }

    const parts = pathSegments.value
    if (parts[0] === 'settings') {
      return 'settings'
    }
    if (parts[0] === 'admin') {
      return 'admin'
    }
    if (parts.length >= 1 && !RESERVED_TOP_LEVEL.has(parts[0]!)) {
      if (parts.length === 1) {
        return 'owner'
      }
      if (parts.length === 2 && parts[1] === 'members') {
        return 'owner'
      }
      if (parts.length >= 2) {
        return 'repo'
      }
    }

    return 'global'
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
