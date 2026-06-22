<script setup lang="ts">
defineProps<{
  open: boolean
}>()

defineEmits<{
  toggle: []
}>()
</script>

<template>
  <aside
    class="hidden shrink-0 self-start border-r transition-[width] duration-200 ease-in-out md:sticky md:top-[var(--ogb-header-height)] md:block md:h-[calc(100dvh-var(--ogb-header-height))]"
    :class="open ? 'w-[var(--ogb-sidebar-width)]' : 'w-[var(--ogb-sidebar-collapsed-width)]'"
    style="border-color: var(--ogb-border); background-color: var(--ogb-surface);"
    data-testid="app-sidebar"
  >
    <AppSidebarPanel
      :expanded="open"
      @expand="$emit('toggle')"
    />
  </aside>
</template>
