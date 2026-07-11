<script setup lang="ts">
import { ciDocsPages } from '~/data/ciDocsPages'

await navigateTo(`/docs/ci/${ciDocsPages[0]?.slug ?? 'overview'}`, { redirectCode: 302 })
</script>
