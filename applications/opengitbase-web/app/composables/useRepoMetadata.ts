import type { Repository } from '~/utils/api'

export function useRepoMetadata() {
  const route = useRoute()
  const api = useApi()

  const owner = computed(() => String(route.params.owner))
  const repoSlug = computed(() => String(route.params.repo))

  const repo = ref<Repository | null>(null)
  const loading = ref(true)
  const notFound = ref(false)

  async function loadRepo(): Promise<void> {
    loading.value = true
    notFound.value = false

    const result = await api.repositories.getBySlug(owner.value, repoSlug.value)
    if (result.data) {
      repo.value = result.data
      loading.value = false
      return
    }

    const contentProbe = await api.repositoryContent.getRefs(owner.value, repoSlug.value)
    if (contentProbe.status === 403) {
      notFound.value = true
      loading.value = false
      return
    }
    if (contentProbe.data) {
      repo.value = {
        id: '',
        name: repoSlug.value,
        slug: repoSlug.value,
        ownerUserId: '',
        ownerSlug: owner.value,
        isPrivate: false,
      }
      loading.value = false
      return
    }

    const fallback = await api.repositories.list()
    const match = fallback.data?.find(item =>
      item.slug === repoSlug.value && (item.ownerSlug === owner.value || item.ownerUserId),
    )
    if (match) {
      repo.value = match
    }
    else {
      notFound.value = true
    }
    loading.value = false
  }

  return {
    owner,
    repoSlug,
    repo,
    loading,
    notFound,
    loadRepo,
  }
}
