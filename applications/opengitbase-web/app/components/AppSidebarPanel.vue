<script setup lang="ts">
import {
  isRepoCodeRoute,
  matchesSidebarRoute,
} from '~/composables/useSidebarContext'

const props = defineProps<{
  expanded: boolean
  guestMobile?: boolean
}>()

defineEmits<{
  expand: []
}>()

const { t } = useI18n()
const auth = useAuth()
const route = useRoute()
const { deployVersion, deploySha } = useInstanceBranding()
const { config: gitConfig, load: loadGitConfig } = useGitConfig()
const { context, ownerSlug, repoSlug } = useSidebarContext()
const {
  sidebarRepos,
  sidebarOrgs,
  hasMoreRepos,
  hasMoreOrgs,
  ownerProfile,
  ownerRepos,
  isOrgMember,
  loading,
} = useSidebarWorkspace()

onMounted(() => {
  void loadGitConfig()
})

const createMenuItems = computed(() => [
  [
    { label: t('repo.create.title'), to: '/repos/new', icon: 'i-lucide-folder-git-2' },
    { label: t('org.create.title'), to: '/orgs/new', icon: 'i-lucide-building-2' },
  ],
])

const settingsNavItems = computed(() => {
  const items = [
    { label: t('settings.profile.title'), to: '/settings', icon: 'i-lucide-user', exact: true },
    { label: t('settings.accessTokens.link'), to: '/settings/access-tokens', icon: 'i-lucide-key-round' },
  ]
  if (gitConfig.value?.sshEnabled) {
    items.push({
      label: t('settings.sshKeys.title'),
      to: '/settings/ssh-keys',
      icon: 'i-lucide-key',
    })
  }
  return items
})

const adminNavItems = computed(() => [
  { label: t('admin.title'), to: '/admin', icon: 'i-lucide-shield', exact: true },
  { label: t('admin.storage.title'), to: '/admin/storage', icon: 'i-lucide-server' },
  { label: t('admin.replication.title'), to: '/admin/repositories', icon: 'i-lucide-database-backup' },
])

const repoNavItems = computed(() => {
  if (!ownerSlug.value || !repoSlug.value) {
    return []
  }
  const owner = ownerSlug.value
  const repo = repoSlug.value
  return [
    {
      label: t('sidebar.repo.code'),
      to: `/${owner}/${repo}`,
      icon: 'i-lucide-code',
      active: isRepoCodeRoute(route.path, owner, repo),
    },
    {
      label: t('repo.discussions.heading'),
      to: `/${owner}/${repo}/discussions`,
      icon: 'i-lucide-messages-square',
      active: matchesSidebarRoute(route.path, `/${owner}/${repo}/discussions`),
    },
    {
      label: t('repo.settings.title'),
      to: `/${owner}/${repo}/settings`,
      icon: 'i-lucide-settings',
      active: matchesSidebarRoute(route.path, `/${owner}/${repo}/settings`, true),
    },
    {
      label: t('repo.members.title'),
      to: `/${owner}/${repo}/members`,
      icon: 'i-lucide-users',
      active: matchesSidebarRoute(route.path, `/${owner}/${repo}/members`, true),
    },
  ]
})

const ownerNavItems = computed(() => {
  if (!ownerSlug.value) {
    return []
  }
  const owner = ownerSlug.value
  const items = [
    {
      label: t('sidebar.owner.overview'),
      to: `/${owner}`,
      icon: 'i-lucide-layout-grid',
      active: route.path === `/${owner}`,
    },
  ]
  if (ownerProfile.value?.kind === 'organization' && isOrgMember.value) {
    items.push({
      label: t('org.members.title'),
      to: `/${owner}/members`,
      icon: 'i-lucide-users',
      active: route.path === `/${owner}/members`,
    })
  }
  return items
})

function navActive(to: string, exact = false): boolean {
  return matchesSidebarRoute(route.path, to, exact)
}

function repoOwnerSlug(repo: { ownerSlug?: string, ownerUserId: string }): string {
  return repo.ownerSlug ?? repo.ownerUserId
}

function navButtonClass(active: boolean): string {
  return active ? 'bg-[var(--ogb-bg)]' : ''
}
</script>

<template>
  <div
    class="flex h-full flex-col p-3"
    data-testid="sidebar-panel"
  >
    <!-- Guest mobile drawer -->
    <nav
      v-if="guestMobile"
      class="min-h-0 flex-1 space-y-1"
    >
      <UButton
        to="/explore"
        icon="i-lucide-compass"
        color="neutral"
        variant="ghost"
        block
        class="justify-start"
        :class="navButtonClass(navActive('/explore'))"
      >
        {{ t('nav.explore') }}
      </UButton>
      <UButton
        to="/sign-in"
        icon="i-lucide-log-in"
        color="neutral"
        variant="ghost"
        block
        class="justify-start"
      >
        {{ t('nav.signIn') }}
      </UButton>
    </nav>

    <template v-else>
      <!-- Context header (expanded) -->
      <div
        v-if="expanded && context === 'repo' && ownerSlug && repoSlug"
        class="mb-3 shrink-0 px-2"
      >
        <p class="truncate font-mono text-xs text-[var(--ogb-text-muted)]">
          {{ ownerSlug }}/{{ repoSlug }}
        </p>
      </div>

      <div
        v-else-if="expanded && context === 'owner' && ownerProfile"
        class="mb-3 shrink-0 px-2"
      >
        <p class="truncate text-sm font-semibold">
          {{ ownerProfile.name }}
        </p>
        <p class="text-xs text-[var(--ogb-text-muted)]">
          {{ ownerProfile.kind === 'organization' ? t('profile.org') : t('profile.user') }}
        </p>
      </div>

      <div
        v-else-if="expanded && context === 'settings'"
        class="mb-3 shrink-0 px-2"
      >
        <p class="text-xs font-medium uppercase tracking-wider text-[var(--ogb-text-muted)]">
          {{ t('settings.title') }}
        </p>
      </div>

      <div
        v-else-if="expanded && context === 'admin'"
        class="mb-3 shrink-0 px-2"
      >
        <p class="text-xs font-medium uppercase tracking-wider text-[var(--ogb-text-muted)]">
          {{ t('admin.title') }}
        </p>
      </div>

      <nav class="min-h-0 flex-1 space-y-1 overflow-y-auto">
        <!-- Global context -->
        <template v-if="context === 'global'">
          <UDropdownMenu
            v-if="expanded"
            :items="createMenuItems"
          >
            <UButton
              icon="i-lucide-plus"
              color="neutral"
              variant="soft"
              block
              class="justify-start"
              trailing-icon="i-lucide-chevron-down"
            >
              {{ t('sidebar.create') }}
            </UButton>
          </UDropdownMenu>

          <UDropdownMenu
            v-else
            :items="createMenuItems"
          >
            <UButton
              icon="i-lucide-plus"
              color="neutral"
              variant="ghost"
              class="mx-auto"
              :aria-label="t('sidebar.create')"
            />
          </UDropdownMenu>

          <template v-if="expanded">
            <div
              v-if="sidebarOrgs.length"
              class="pt-4"
            >
              <p class="mb-1 px-2 text-xs font-medium uppercase tracking-wider text-[var(--ogb-text-muted)]">
                {{ t('dashboard.organizations') }}
              </p>
              <UButton
                v-for="org in sidebarOrgs"
                :key="org.id"
                :to="`/${org.slug ?? org.name}`"
                icon="i-lucide-building-2"
                color="neutral"
                variant="ghost"
                block
                class="justify-start"
                :class="navButtonClass(navActive(`/${org.slug ?? org.name}`))"
              >
                {{ org.name }}
              </UButton>
              <UButton
                v-if="hasMoreOrgs"
                to="/"
                color="neutral"
                variant="ghost"
                block
                class="justify-start text-[var(--ogb-text-muted)]"
                size="sm"
              >
                {{ t('sidebar.viewAll') }}
              </UButton>
            </div>

            <div
              v-if="loading && !sidebarRepos.length"
              class="px-2 pt-4 text-sm text-[var(--ogb-text-muted)]"
            >
              {{ t('common.loading') }}
            </div>

            <div
              v-else-if="sidebarRepos.length"
              class="pt-4"
            >
              <p class="mb-1 px-2 text-xs font-medium uppercase tracking-wider text-[var(--ogb-text-muted)]">
                {{ t('dashboard.repositories') }}
              </p>
              <UButton
                v-for="repo in sidebarRepos"
                :key="repo.id"
                :to="`/${repoOwnerSlug(repo)}/${repo.slug}`"
                icon="i-lucide-git-branch"
                color="neutral"
                variant="ghost"
                block
                class="justify-start"
                :class="navButtonClass(navActive(`/${repoOwnerSlug(repo)}/${repo.slug}`))"
              >
                <span class="truncate">{{ repo.name }}</span>
              </UButton>
              <UButton
                v-if="hasMoreRepos"
                to="/"
                color="neutral"
                variant="ghost"
                block
                class="justify-start text-[var(--ogb-text-muted)]"
                size="sm"
              >
                {{ t('sidebar.viewAll') }}
              </UButton>
            </div>
          </template>
        </template>

        <!-- Repo context -->
        <template v-else-if="context === 'repo'">
          <UButton
            v-for="item in repoNavItems"
            :key="item.to"
            :to="item.to"
            :icon="item.icon"
            color="neutral"
            variant="ghost"
            :block="expanded"
            :class="expanded ? `justify-start ${navButtonClass(item.active)}` : `mx-auto ${navButtonClass(item.active)}`"
            :title="expanded ? undefined : item.label"
          >
            <span v-if="expanded">{{ item.label }}</span>
          </UButton>
        </template>

        <!-- Owner context -->
        <template v-else-if="context === 'owner'">
          <UButton
            v-for="item in ownerNavItems"
            :key="item.to"
            :to="item.to"
            :icon="item.icon"
            color="neutral"
            variant="ghost"
            :block="expanded"
            :class="expanded ? `justify-start ${navButtonClass(item.active)}` : `mx-auto ${navButtonClass(item.active)}`"
            :title="expanded ? undefined : item.label"
          >
            <span v-if="expanded">{{ item.label }}</span>
          </UButton>

          <template v-if="expanded && ownerRepos.length">
            <p class="mb-1 mt-4 px-2 text-xs font-medium uppercase tracking-wider text-[var(--ogb-text-muted)]">
              {{ t('profile.repositories') }}
            </p>
            <UButton
              v-for="repo in ownerRepos"
              :key="repo.id"
              :to="`/${ownerSlug}/${repo.slug}`"
              icon="i-lucide-git-branch"
              color="neutral"
              variant="ghost"
              block
              class="justify-start"
              :class="navButtonClass(navActive(`/${ownerSlug}/${repo.slug}`))"
            >
              <span class="truncate">{{ repo.name }}</span>
            </UButton>
          </template>
        </template>

        <!-- Settings context -->
        <template v-else-if="context === 'settings'">
          <UButton
            v-for="item in settingsNavItems"
            :key="item.to"
            :to="item.to"
            :icon="item.icon"
            color="neutral"
            variant="ghost"
            :block="expanded"
            :class="expanded ? `justify-start ${navButtonClass(navActive(item.to, item.exact))}` : `mx-auto ${navButtonClass(navActive(item.to, item.exact))}`"
            :title="expanded ? undefined : item.label"
          >
            <span v-if="expanded">{{ item.label }}</span>
          </UButton>
        </template>

        <!-- Admin context -->
        <template v-else-if="context === 'admin'">
          <UButton
            v-for="item in adminNavItems"
            :key="item.to"
            :to="item.to"
            :icon="item.icon"
            color="neutral"
            variant="ghost"
            :block="expanded"
            :class="expanded ? `justify-start ${navButtonClass(navActive(item.to, item.exact))}` : `mx-auto ${navButtonClass(navActive(item.to, item.exact))}`"
            :title="expanded ? undefined : item.label"
          >
            <span v-if="expanded">{{ item.label }}</span>
          </UButton>
        </template>
      </nav>

      <UButton
        v-if="!expanded && !guestMobile"
        icon="i-lucide-panel-left-open"
        color="neutral"
        variant="ghost"
        class="mx-auto shrink-0"
        :aria-label="t('nav.toggleSidebar')"
        @click="$emit('expand')"
      />

      <div
        v-if="expanded && deployVersion"
        class="shrink-0 px-2 pb-1 pt-4"
        data-testid="sidebar-deploy-version"
      >
        <p class="text-xs text-[var(--ogb-text-muted)]">
          {{ deployVersion }}
        </p>
        <p
          v-if="deploySha"
          class="text-[10px] leading-tight text-[var(--ogb-text-muted)] opacity-70"
        >
          {{ deploySha }}
        </p>
      </div>
    </template>
  </div>
</template>
