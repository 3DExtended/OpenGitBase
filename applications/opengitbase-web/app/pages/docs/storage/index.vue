<script setup lang="ts">
import { storageDocsPages } from '~/data/storageDocsPages'

await navigateTo(`/docs/storage/${storageDocsPages[0]?.slug ?? 'org-storage-nodes'}`, { redirectCode: 302 })
</script>
