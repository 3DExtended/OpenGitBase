<script setup lang="ts">
import DiscussionDetailLayout from '~/components/discussions/DiscussionDetailLayout.vue'

const ctx = useDiscussionDetailPage()
</script>

<template>
  <DiscussionDetailLayout :ctx="ctx" />
</template>
