<script setup lang="ts">
import type { Repository } from '~/utils/api'

const { t } = useI18n()
const api = useApi()

const query = ref('')
const repos = ref<Repository[]>([])
const loading = ref(true)

useHead({ title: t('explore.title') })

async function search() {
  loading.value = true
  const result = await api.discovery.listPublic({ q: query.value || undefined })
  repos.value = result.data ?? []
  loading.value = false
}

onMounted(search)

let debounceTimer: ReturnType<typeof setTimeout> | null = null
watch(query, () => {
  if (debounceTimer) {
    clearTimeout(debounceTimer)
  }
  debounceTimer = setTimeout(search, 300)
})
</script>

<template>
  <div class="mx-auto max-w-5xl space-y-6">
    <div>
      <h1 class="text-2xl font-semibold">
        {{ t('explore.title') }}
      </h1>
      <p class="mt-1 text-sm text-[var(--ogb-text-muted)]">
        {{ t('explore.subtitle') }}
      </p>
    </div>

    <UInput
      v-model="query"
      :placeholder="t('explore.searchPlaceholder')"
      icon="i-lucide-search"
      size="lg"
    />

    <div
      v-if="loading"
      class="text-sm text-[var(--ogb-text-muted)]"
    >
      {{ t('common.loading') }}
    </div>

    <p
      v-else-if="!repos.length"
      class="text-sm text-[var(--ogb-text-muted)]"
    >
      {{ t('explore.empty') }}
    </p>

    <div
      v-else
      class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3"
    >
      <RepoCard
        v-for="repo in repos"
        :key="repo.id"
        :repo="repo"
        :owner-slug="repo.ownerSlug"
      />
    </div>
  </div>
</template>
