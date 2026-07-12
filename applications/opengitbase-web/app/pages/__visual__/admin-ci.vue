<script setup lang="ts">
definePageMeta({
  layout: false,
  middleware: 'visual-dev-only',
})
</script>

<template>
  <div
    class="p-6"
    style="background-color: var(--ogb-bg);"
    data-testid="visual-admin-ci"
  >
    <AdminCiVisualFixture />
  </div>
</template>
