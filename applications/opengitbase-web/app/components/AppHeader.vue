<script setup lang="ts">
defineProps<{
  instanceName: string
  instanceLogoUrl: string
}>()

defineEmits<{
  toggleSidebar: []
}>()

const { t } = useI18n()
const auth = useAuth()
const { context } = useSidebarContext()
const colorMode = useColorMode()

const showSidebarToggle = computed(() => auth.isAuthenticated || context.value === 'repo')

const navLinks = [
  { labelKey: 'nav.home', to: '/' },
  { labelKey: 'nav.explore', to: '/explore' },
  { labelKey: 'nav.pitch', to: '/pitch' },
]

function toggleTheme() {
  colorMode.preference = colorMode.value === 'dark' ? 'light' : 'dark'
}

const userMenuItems = computed(() => {
  const items: Array<Array<{ label: string, to: string, icon: string }>> = []
  if (auth.isAdmin) {
    items.push([{ label: t('admin.nav'), to: '/admin', icon: 'i-lucide-shield' }])
  }
  items.push(
    [{ label: t('settings.title'), to: '/settings', icon: 'i-lucide-settings' }],
    [{ label: t('nav.signOut'), to: '/sign-out', icon: 'i-lucide-log-out' }],
  )
  return items
})
</script>

<template>
  <header
    class="sticky top-0 z-50 border-b backdrop-blur-md"
    style="border-color: var(--ogb-border); background-color: color-mix(in srgb, var(--ogb-surface) 85%, transparent); height: var(--ogb-header-height);"
  >
    <div class="mx-auto flex h-full max-w-screen-2xl items-center gap-3 px-4 md:px-6">
      <UButton
        icon="i-lucide-panel-left"
        color="neutral"
        variant="ghost"
        class="inline-flex"
        :class="{ 'md:hidden': !showSidebarToggle }"
        :aria-label="t('nav.toggleSidebar')"
        @click="$emit('toggleSidebar')"
      />

      <NuxtLink
        to="/"
        class="flex min-w-0 items-center gap-2.5 font-semibold tracking-tight"
      >
        <img
          v-if="instanceLogoUrl"
          :src="instanceLogoUrl"
          :alt="instanceName"
          class="size-7 shrink-0 rounded-md object-contain"
        >
        <UIcon
          v-else
          name="i-lucide-git-branch"
          class="size-6 shrink-0 text-[var(--ogb-accent)]"
        />
        <span class="truncate">{{ instanceName }}</span>
      </NuxtLink>

      <nav class="hidden items-center gap-1 md:ml-6 md:flex">
        <UButton
          v-for="link in navLinks"
          :key="link.to"
          :to="link.to"
          color="neutral"
          variant="ghost"
          size="sm"
        >
          {{ t(link.labelKey) }}
        </UButton>
      </nav>

      <div class="ml-auto flex items-center gap-1">
        <UButton
          icon="i-lucide-sun-moon"
          color="neutral"
          variant="ghost"
          :aria-label="t('nav.toggleTheme')"
          @click="toggleTheme"
        />

        <template v-if="auth.isAuthenticated">
          <NotificationBell />
          <UDropdownMenu :items="userMenuItems">
            <UButton
              color="neutral"
              variant="ghost"
              size="sm"
              trailing-icon="i-lucide-chevron-down"
            >
              {{ auth.user?.username }}
            </UButton>
          </UDropdownMenu>
        </template>

        <template v-else>
          <UButton
            to="/sign-in"
            color="neutral"
            variant="ghost"
            size="sm"
            class="hidden sm:inline-flex"
          >
            {{ t('nav.signIn') }}
          </UButton>
          <UButton
            to="/sign-up"
            color="primary"
            size="sm"
          >
            {{ t('nav.signUp') }}
          </UButton>
        </template>
      </div>
    </div>
  </header>
</template>
