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

const features = computed(() => [
  { icon: 'i-lucide-code', title: t('home.features.repositories.title'), body: t('home.features.repositories.body') },
  { icon: 'i-lucide-workflow', title: t('home.features.ci.title'), body: t('home.features.ci.body') },
  { icon: 'i-lucide-server', title: t('home.features.storage.title'), body: t('home.features.storage.body') },
])

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
  <div>
    <div
      v-if="auth.isAuthenticated"
      class="mx-auto max-w-5xl space-y-8"
    >
      <EmailVerificationBanner />
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
            :owner-slug="repo.ownerSlug"
          />
        </div>
      </section>
    </div>

    <div
      v-else
      class="max-w-7xl space-y-12"
    >
      <section class="relative overflow-hidden pb-6 pt-6 sm:pt-10">
        <div
          class="pointer-events-none absolute -top-24 right-0 h-80 w-[32rem] rounded-full"
          style="background: radial-gradient(circle, color-mix(in srgb, var(--ogb-accent) 16%, transparent), transparent 68%);"
          aria-hidden="true"
        />
        <p class="relative font-mono text-sm text-[var(--ogb-accent)]">
          {{ t('home.heroPrompt') }}
        </p>
        <i18n-t
          keypath="home.heroTitle"
          tag="h1"
          class="relative mt-4 max-w-2xl text-4xl font-bold leading-[1.02] tracking-tight sm:text-[4rem]"
        >
          <template #accent>
            <span class="text-[var(--ogb-accent)]">{{ t('home.heroTitleAccent') }}</span>
          </template>
          <template #linebreak>
            <br>
          </template>
        </i18n-t>
        <p class="relative mt-5 max-w-[40rem] text-lg text-[var(--ogb-text-muted)]">
          {{ t('home.heroSubtitle') }}
        </p>
        <div class="relative mt-7 flex flex-wrap gap-3">
          <UButton
            to="/sign-up"
            size="lg"
            icon="i-lucide-arrow-right"
          >
            {{ t('home.getStarted') }}
          </UButton>
          <UButton
            to="/docs"
            size="lg"
            variant="outline"
          >
            {{ t('home.readDocs') }}
          </UButton>
        </div>
        <p class="relative mt-4 font-mono text-xs text-[var(--ogb-text-muted)]">
          {{ t('home.heroInstall') }} · {{ t('home.heroInstallNote') }}
        </p>
      </section>

      <section class="grid gap-4 sm:grid-cols-3">
        <div
          v-for="feature in features"
          :key="feature.title"
          class="rounded-xl border p-5"
          style="border-color: var(--ogb-border); background: var(--ogb-surface);"
        >
          <UIcon
            :name="feature.icon"
            class="size-6 text-[var(--ogb-accent)]"
          />
          <h3 class="mt-3 font-semibold">
            {{ feature.title }}
          </h3>
          <p class="mt-1.5 text-sm text-[var(--ogb-text-muted)]">
            {{ feature.body }}
          </p>
        </div>
      </section>

      <section v-if="!loading && recentPublic.length">
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

        <div class="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
          <RepoCard
            v-for="repo in recentPublic"
            :key="repo.id"
            :repo="repo"
            :owner-slug="repo.ownerSlug"
          />
        </div>
      </section>
    </div>
  </div>
</template>
