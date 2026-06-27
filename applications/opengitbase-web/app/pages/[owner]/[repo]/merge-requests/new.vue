<script setup lang="ts">
const route = useRoute()
const { t } = useI18n()
const api = useApi()

const owner = computed(() => String(route.params.owner))
const repoSlug = computed(() => String(route.params.repo))

const refs = ref<string[]>([])
const title = ref('')
const body = ref('')
const sourceRef = ref('')
const targetRef = ref('')
const isDraft = ref(false)
const loading = ref(true)
const saving = ref(false)
const error = ref<string | null>(null)

onMounted(async () => {
  loading.value = true
  const refsResult = await api.repositoryContent.getRefs(owner.value, repoSlug.value)
  const branches = refsResult.data?.branches.map(branch => branch.name) ?? []
  refs.value = branches
  sourceRef.value = String(route.query.source ?? branches.find(b => b !== refsResult.data?.defaultRef) ?? branches[0] ?? '')
  targetRef.value = String(route.query.target ?? refsResult.data?.defaultRef ?? branches[0] ?? '')
  loading.value = false
})

async function submit(): Promise<void> {
  saving.value = true
  error.value = null
  const result = await api.mergeRequests.create(owner.value, repoSlug.value, {
    title: title.value.trim(),
    body: body.value.trim() || null,
    sourceRef: sourceRef.value,
    targetRef: targetRef.value,
    isDraft: isDraft.value,
  })
  saving.value = false
  if (result.error || !result.data) {
    error.value = result.error ?? t('repo.mergeRequests.createFailed')
    return
  }
  await navigateTo(`/${owner.value}/${repoSlug.value}/merge-requests/${result.data.number}`)
}
</script>

<template>
  <div class="mx-auto max-w-3xl space-y-4">
    <UButton
      :to="`/${owner}/${repoSlug}/merge-requests`"
      variant="ghost"
      icon="i-lucide-arrow-left"
      size="sm"
    >
      {{ t('repo.mergeRequests.title') }}
    </UButton>

    <h1 class="text-2xl font-semibold">
      {{ t('repo.mergeRequests.new') }}
    </h1>

    <UCard v-if="loading">
      <p class="text-sm text-[var(--ogb-text-muted)]">
        {{ t('common.loading') }}
      </p>
    </UCard>

    <UCard v-else>
      <form
        class="space-y-4"
        @submit.prevent="submit"
      >
        <UFormField
          :label="t('repo.mergeRequests.fields.title')"
          required
        >
          <UInput v-model="title" />
        </UFormField>

        <div class="grid gap-4 sm:grid-cols-2">
          <UFormField
            :label="t('repo.mergeRequests.fields.source')"
            required
          >
            <USelect
              v-model="sourceRef"
              :items="refs"
            />
          </UFormField>
          <UFormField
            :label="t('repo.mergeRequests.fields.target')"
            required
          >
            <USelect
              v-model="targetRef"
              :items="refs"
            />
          </UFormField>
        </div>

        <UCheckbox
          v-model="isDraft"
          :label="t('repo.mergeRequests.fields.draft')"
        />

        <UFormField :label="t('repo.mergeRequests.fields.body')">
          <CollaborationMarkdownEditor
            v-model="body"
            min-height="8rem"
          />
        </UFormField>

        <UAlert
          v-if="error"
          color="error"
          variant="subtle"
          :description="error"
        />

        <UButton
          type="submit"
          :loading="saving"
          :disabled="!title.trim() || !sourceRef || !targetRef || sourceRef === targetRef"
        >
          {{ t('repo.mergeRequests.create') }}
        </UButton>
      </form>
    </UCard>
  </div>
</template>
