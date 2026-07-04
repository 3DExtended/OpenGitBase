<script setup lang="ts">
import { repoCommitPath } from '~/utils/repoBrowse'

const props = withDefaults(defineProps<{
  owner: string
  repo: string
  sha: string
  from?: string
  display?: 'short' | 'full'
}>(), {
  display: 'short',
})

const href = computed(() =>
  repoCommitPath(props.owner, props.repo, props.sha, props.from),
)

const label = computed(() =>
  props.display === 'full' ? props.sha : props.sha.slice(0, 8),
)
</script>

<template>
  <NuxtLink
    :to="href"
    class="font-mono text-[var(--ogb-accent)] hover:underline"
  >
    {{ label }}
  </NuxtLink>
</template>
