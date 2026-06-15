<script setup lang="ts">
definePageMeta({
  middleware: 'auth',
})

const auth = useAuth()

await auth.signOut()
await navigateTo('/')
</script>

<template>
  <div />
</template>
