<script setup lang="ts">
import type { Repository } from '~/utils/api'

const { instanceName } = useInstanceBranding()
const { t } = useI18n()
const auth = useAuth()
const api = useApi()

const repos = ref<Repository[]>([])
const recentPublic = ref<Repository[]>([])
const orgs = ref<Array<{ id: string, name: string }>>([])
const loading = ref(true)

useHead({
  title: instanceName,
})

onMounted(async () => {
  loading.value = true
  if (auth.isAuthenticated) {
    const [repoResult, orgResult] = await Promise.all([
      api.repositories.list(),
      api.organizations.list(),
    ])
    repos.value = repoResult.data ?? []
    orgs.value = orgResult.data ?? []
  }
  else {
    const feed = await api.discovery.recentFeed()
    recentPublic.value = feed.data ?? []
  }
  loading.value = false
})
</script>

<template>
  <div class="mx-auto max-w-5xl space-y-8">
    <EmailVerificationBanner v-if="auth.isAuthenticated" />

    <template v-if="auth.isAuthenticated">
      <div class="flex flex-wrap items-center justify-between gap-4">
        <div>
          <h1 class="text-2xl font-semibold tracking-tight">
            {{ t('dashboard.title', { username: auth.user?.username }) }}
          </h1>
          <p class="mt-1 text-sm text-[var(--ogb-text-muted)]">
            {{ t('dashboard.subtitle') }}
          </p>
        </div>
        <div class="flex flex-wrap gap-2">
          <UButton
            to="/repos/new"
            icon="i-lucide-plus"
            :disabled="!auth.isEmailVerified"
          >
            {{ t('repo.create.title') }}
          </UButton>
          <UButton
            to="/orgs/new"
            variant="soft"
            icon="i-lucide-building-2"
          >
            {{ t('org.create.title') }}
          </UButton>
        </div>
      </div>

      <section v-if="orgs.length">
        <h2 class="mb-4 text-lg font-semibold">
          {{ t('dashboard.organizations') }}
        </h2>
        <div class="flex flex-wrap gap-2">
          <UButton
            v-for="org in orgs"
            :key="org.id"
            :to="`/${org.name}`"
            variant="soft"
            size="sm"
          >
            {{ org.name }}
          </UButton>
        </div>
      </section>

      <section>
        <h2 class="mb-4 text-lg font-semibold">
          {{ t('dashboard.repositories') }}
        </h2>

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
          {{ t('dashboard.noRepositories') }}
        </p>

        <div
          v-else
          class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3"
        >
          <RepoCard
            v-for="repo in repos"
            :key="repo.id"
            :repo="repo"
            :owner-slug="auth.user?.username"
          />
        </div>
      </section>
    </template>

    <template v-else>
      <section
        class="rounded-2xl border px-6 py-10 sm:px-10"
        style="border-color: var(--ogb-border); background: linear-gradient(135deg, color-mix(in srgb, var(--ogb-accent) 8%, var(--ogb-surface)), var(--ogb-surface));"
      >
        <h1 class="text-3xl font-semibold tracking-tight sm:text-4xl">
          {{ t('home.welcome', { instanceName }) }}
        </h1>
        <p class="mt-3 max-w-2xl text-[var(--ogb-text-muted)]">
          {{ t('home.description') }}
        </p>
        <div class="mt-6 flex flex-wrap gap-3">
          <UButton
            to="/sign-up"
            size="lg"
          >
            {{ t('home.getStarted') }}
          </UButton>
          <UButton
            to="/explore"
            size="lg"
            variant="soft"
          >
            {{ t('nav.explore') }}
          </UButton>
        </div>
      </section>

      <section>
        <div class="mb-4 flex items-center justify-between">
          <h2 class="text-lg font-semibold">
            {{ t('home.recentPublic') }}
          </h2>
          <UButton
            to="/explore"
            variant="ghost"
            size="sm"
          >
            {{ t('home.viewAll') }}
          </UButton>
        </div>

        <div
          v-if="loading"
          class="text-sm text-[var(--ogb-text-muted)]"
        >
          {{ t('common.loading') }}
        </div>

        <p
          v-else-if="!recentPublic.length"
          class="text-sm text-[var(--ogb-text-muted)]"
        >
          {{ t('home.noPublicRepos') }}
        </p>

        <div
          v-else
          class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3"
        >
          <RepoCard
            v-for="repo in recentPublic"
            :key="repo.id"
            :repo="repo"
            :owner-slug="repo.ownerSlug"
          />
        </div>
      </section>
    </template>
  </div>
</template>
