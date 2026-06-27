<script setup lang="ts">
const props = defineProps<{
  owner: string
  repoSlug: string
  refName: string
}>()

const { t } = useI18n()
const auth = useAuth()
const api = useApi()

const aheadCount = ref(0)
const defaultRef = ref<string | null>(null)
const hasActiveMergeRequest = ref(false)

const visible = computed(() =>
  auth.isAuthenticated
  && aheadCount.value > 0
  && !hasActiveMergeRequest.value
  && defaultRef.value
  && props.refName !== defaultRef.value,
)

onMounted(async () => {
  if (!auth.isAuthenticated) {
    return
  }
  const result = await api.mergeRequests.getBranchAheadSummary(props.owner, props.repoSlug, props.refName)
  aheadCount.value = result.data?.aheadCount ?? 0
  defaultRef.value = result.data?.defaultRef ?? null
  hasActiveMergeRequest.value = result.data?.hasActiveMergeRequest ?? false
})
</script>

<template>
  <UAlert
    v-if="visible"
    color="info"
    variant="subtle"
    icon="i-lucide-git-pull-request-create"
    :title="t('repo.mergeRequests.bannerTitle')"
    :description="t('repo.mergeRequests.bannerDescription', { branch: refName, count: aheadCount })"
  >
    <template #actions>
      <UButton
        size="xs"
        :to="`/${owner}/${repoSlug}/merge-requests/new?source=${encodeURIComponent(refName)}&target=${encodeURIComponent(defaultRef ?? '')}`"
      >
        {{ t('repo.mergeRequests.create') }}
      </UButton>
    </template>
  </UAlert>
</template>
