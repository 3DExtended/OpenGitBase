<script setup lang="ts">
import type { OwnerProfile } from '~/utils/api'

const route = useRoute()
const { t } = useI18n()
const api = useApi()

const owner = computed(() => String(route.params.owner))
const profile = ref<OwnerProfile | null>(null)
const loading = ref(true)
const notFound = ref(false)

useHead({
  title: computed(() => profile.value?.name ?? owner.value),
})

onMounted(async () => {
  loading.value = true
  const result = await api.discovery.getProfile(owner.value)
  if (result.status === 404 || !result.data) {
    notFound.value = true
    loading.value = false
    return
  }
  profile.value = result.data
  loading.value = false
})
</script>

<template>
  <div class="mx-auto max-w-4xl space-y-6">
    <div
      v-if="loading"
      class="text-sm text-[var(--ogb-text-muted)]"
    >
      {{ t('common.loading') }}
    </div>

    <UCard v-else-if="notFound">
      <p class="text-sm text-[var(--ogb-text-muted)]">
        {{ t('profile.notFound') }}
      </p>
    </UCard>

    <template v-else-if="profile">
      <div class="flex flex-wrap items-center gap-3">
        <h1 class="text-2xl font-semibold">
          {{ profile.name }}
        </h1>
        <ProfileTypeBadge :kind="profile.kind" />
      </div>

      <p
        v-if="profile.bio"
        class="text-sm text-[var(--ogb-text-muted)]"
      >
        {{ profile.bio }}
      </p>

      <section>
        <h2 class="mb-4 text-lg font-semibold">
          {{ t('profile.repositories') }}
        </h2>

        <div
          v-if="!profile.repositories.length"
          class="text-sm text-[var(--ogb-text-muted)]"
        >
          {{ t('profile.noRepositories') }}
        </div>

        <div
          v-else
          class="grid gap-4 sm:grid-cols-2"
        >
          <RepoCard
            v-for="repo in profile.repositories"
            :key="repo.id"
            :repo="repo"
            :owner-slug="owner"
          />
        </div>
      </section>
    </template>
  </div>
</template>
