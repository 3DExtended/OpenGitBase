<script setup lang="ts">
import { getCiDocPage } from '~/data/ciDocsPages'

const route = useRoute()
const slug = String(route.params.slug)
const page = getCiDocPage(slug)

if (!page) {
  throw createError({ statusCode: 404, statusMessage: 'Documentation page not found' })
}
</script>

<template>
  <DocsCiArticle :page="page" />
</template>
