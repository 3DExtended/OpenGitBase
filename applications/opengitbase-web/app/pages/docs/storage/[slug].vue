<script setup lang="ts">
import { getStorageDocPage } from '~/data/storageDocsPages'

const route = useRoute()
const slug = String(route.params.slug)
const page = getStorageDocPage(slug)

if (!page) {
  throw createError({ statusCode: 404, statusMessage: 'Documentation page not found' })
}
</script>

<template>
  <DocsStorageArticle :page="page" />
</template>
