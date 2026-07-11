import type { Organization, OwnerProfile, Repository } from '~/utils/api'

const REPO_CAP = 8
const ORG_CAP = 4

function sortReposByUpdated(repos: Repository[]): Repository[] {
  return [...repos].sort((a, b) => {
    const aTime = a.updatedAt ? Date.parse(a.updatedAt) : 0
    const bTime = b.updatedAt ? Date.parse(b.updatedAt) : 0
    return bTime - aTime
  })
}

export function useSidebarWorkspace() {
  const api = useApi()
  const auth = useAuth()
  const { context, ownerSlug } = useSidebarContext()

  const repos = useState<Repository[]>('sidebarRepos', () => [])
  const orgs = useState<Organization[]>('sidebarOrgs', () => [])
  const ownerProfile = useState<OwnerProfile | null>('sidebarOwnerProfile', () => null)
  const isOrgMember = useState('sidebarIsOrgMember', () => false)
  const isOrgOwner = useState('sidebarIsOrgOwner', () => false)
  const workspaceLoaded = useState('sidebarWorkspaceLoaded', () => false)
  const loading = ref(false)

  const sidebarRepos = computed(() => sortReposByUpdated(repos.value).slice(0, REPO_CAP))
  const hasMoreRepos = computed(() => repos.value.length > REPO_CAP)

  const sidebarOrgs = computed(() =>
    [...orgs.value]
      .sort((a, b) => a.name.localeCompare(b.name))
      .slice(0, ORG_CAP),
  )
  const hasMoreOrgs = computed(() => orgs.value.length > ORG_CAP)

  const ownerRepos = computed(() => ownerProfile.value?.repositories ?? [])

  async function loadWorkspace(): Promise<void> {
    if (!auth.isAuthenticated || workspaceLoaded.value) {
      return
    }

    loading.value = true
    try {
      const [repoResult, orgResult] = await Promise.all([
        api.repositories.list(),
        api.organizations.list(),
      ])
      repos.value = repoResult.data ?? []
      orgs.value = orgResult.data ?? []
      workspaceLoaded.value = true
    }
    finally {
      loading.value = false
    }
  }

  async function loadOwnerProfile(slug: string): Promise<void> {
    const result = await api.discovery.getProfile(slug)
    ownerProfile.value = result.data

    if (result.data?.kind === 'organization' && auth.isAuthenticated) {
      const orgsResult = await api.organizations.list()
      const organization = orgsResult.data?.find(
        org => (org.slug ?? org.name).toLowerCase() === slug.toLowerCase(),
      )
      isOrgMember.value = organization != null
      isOrgOwner.value = false

      if (organization) {
        const membersResult = await api.organizations.members.list(organization.id)
        const username = auth.user?.username?.toLowerCase()
        isOrgOwner.value = membersResult.data?.some(
          member =>
            member.role === 1
            && member.username?.toLowerCase() === username,
        ) ?? false
      }
    }
    else {
      isOrgMember.value = false
      isOrgOwner.value = false
    }
  }

  watch(
    () => auth.isAuthenticated,
    (authenticated) => {
      if (!authenticated) {
        repos.value = []
        orgs.value = []
        ownerProfile.value = null
        isOrgMember.value = false
        isOrgOwner.value = false
        workspaceLoaded.value = false
      }
    },
  )

  watch(
    [context, ownerSlug, () => auth.isAuthenticated],
    async ([nextContext, slug, authenticated]) => {
      if (!authenticated) {
        return
      }

      if (nextContext === 'global' || nextContext === 'repo') {
        await loadWorkspace()
      }

      if ((nextContext === 'owner' || nextContext === 'repo') && slug) {
        await loadOwnerProfile(slug)
      }
    },
    { immediate: true },
  )

  return {
    sidebarRepos,
    sidebarOrgs,
    hasMoreRepos,
    hasMoreOrgs,
    ownerProfile,
    ownerRepos,
    isOrgMember,
    isOrgOwner,
    loading,
  }
}
