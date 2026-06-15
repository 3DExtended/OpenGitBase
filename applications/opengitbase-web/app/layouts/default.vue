<script setup lang="ts">
const { instanceName, instanceLogoUrl } = useInstanceBranding()

const sidebarOpen = useState('sidebarOpen', () => true)

function toggleSidebar() {
  sidebarOpen.value = !sidebarOpen.value
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
        :open="sidebarOpen"
        @toggle="toggleSidebar"
      />
      <main class="ogb-content">
        <slot />
      </main>
    </div>
  </div>
</template>
