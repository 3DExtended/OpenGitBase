<script setup lang="ts">
defineProps<{
  open: boolean
}>()

defineEmits<{
  toggle: []
}>()

const { t } = useI18n()
const auth = useAuth()
const route = useRoute()
const { config: gitConfig, load: loadGitConfig } = useGitConfig()

onMounted(() => {
  void loadGitConfig()
})

const navItems = computed(() => {
  if (!auth.isAuthenticated) {
    return []
  }
  const items = [
    { label: t('dashboard.repositories'), to: '/', icon: 'i-lucide-layout-dashboard' },
    { label: t('repo.create.title'), to: '/repos/new', icon: 'i-lucide-plus' },
    { label: t('org.create.title'), to: '/orgs/new', icon: 'i-lucide-building-2' },
    { label: t('settings.title'), to: '/settings', icon: 'i-lucide-settings' },
    { label: t('settings.accessTokens.link'), to: '/settings/access-tokens', icon: 'i-lucide-key-round' },
  ]
  if (gitConfig.value?.sshEnabled) {
    items.push({
      label: t('settings.sshKeys.title'),
      to: '/settings/ssh-keys',
      icon: 'i-lucide-key',
    })
  }
  if (auth.isAdmin) {
    items.push({ label: t('admin.nav'), to: '/admin', icon: 'i-lucide-shield' })
  }
  return items
})

function isActive(to: string) {
  return route.path === to || route.path.startsWith(`${to}/`)
}
</script>

<template>
  <aside
    class="hidden shrink-0 border-r transition-[width] duration-200 ease-in-out md:block"
    :class="open ? 'w-[var(--ogb-sidebar-width)]' : 'w-[var(--ogb-sidebar-collapsed-width)]'"
    style="border-color: var(--ogb-border); background-color: var(--ogb-surface);"
  >
    <div class="flex h-full flex-col p-3">
      <nav
        v-if="open && navItems.length"
        class="space-y-1"
      >
        <UButton
          v-for="item in navItems"
          :key="item.to"
          :to="item.to"
          :icon="item.icon"
          color="neutral"
          variant="ghost"
          block
          class="justify-start"
          :class="isActive(item.to) ? 'bg-[var(--ogb-bg)]' : ''"
        >
          {{ item.label }}
        </UButton>
      </nav>

      <div
        v-else-if="open"
        class="px-2 py-1"
      >
        <p class="text-xs font-medium uppercase tracking-wider text-[var(--ogb-text-muted)]">
          {{ t('sidebar.placeholder') }}
        </p>
        <p class="mt-3 text-sm leading-relaxed text-[var(--ogb-text-muted)]">
          {{ t('sidebar.signInPrompt') }}
        </p>
      </div>

      <UButton
        v-if="!open"
        icon="i-lucide-panel-left-open"
        color="neutral"
        variant="ghost"
        class="mx-auto"
        :aria-label="t('nav.toggleSidebar')"
        @click="$emit('toggle')"
      />
    </div>
  </aside>
</template>
