<script setup lang="ts">
const { instanceName, instanceLogoUrl } = useInstanceBranding()
const { t } = useI18n()
const auth = useAuth()
const route = useRoute()

const { context } = useSidebarContext()

const sidebarOpen = useState('sidebarOpen', () => true)
const mobileDrawerOpen = useState('mobileDrawerOpen', () => false)

const showDesktopSidebar = computed(() => auth.isAuthenticated || context.value === 'repo')
const guestMobileDrawer = computed(() => !auth.isAuthenticated && context.value !== 'repo')

watch(() => route.path, () => {
  mobileDrawerOpen.value = false
})

function toggleSidebar() {
  if (import.meta.client && window.matchMedia('(max-width: 767px)').matches) {
    mobileDrawerOpen.value = !mobileDrawerOpen.value
    return
  }
  sidebarOpen.value = !sidebarOpen.value
}

function closeMobileDrawer() {
  mobileDrawerOpen.value = false
}
</script>

<template>
  <div class="ogb-shell">
    <AppHeader
      :instance-name="instanceName"
      :instance-logo-url="instanceLogoUrl"
      @toggle-sidebar="toggleSidebar"
    />
    <div class="ogb-main">
      <AppSidebar
        v-if="showDesktopSidebar"
        :open="sidebarOpen"
        @toggle="toggleSidebar"
      />
      <main class="ogb-content">
        <slot />
      </main>
    </div>

    <Teleport to="body">
      <div
        v-if="mobileDrawerOpen"
        class="fixed inset-0 z-50 md:hidden"
        data-testid="mobile-sidebar-drawer"
      >
        <div
          class="absolute inset-0 bg-black/40"
          aria-hidden="true"
          @click="closeMobileDrawer"
        />
        <aside
          class="absolute left-0 top-0 flex h-full w-[min(var(--ogb-sidebar-width),85vw)] flex-col border-r shadow-xl"
          style="border-color: var(--ogb-border); background-color: var(--ogb-surface); padding-top: var(--ogb-header-height);"
          role="dialog"
          aria-modal="true"
          :aria-label="t('nav.toggleSidebar')"
        >
          <AppSidebarPanel
            :expanded="true"
            :guest-mobile="guestMobileDrawer"
          />
        </aside>
      </div>
    </Teleport>

    <AppSiteFooter />
  </div>
</template>
