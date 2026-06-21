<script setup lang="ts">
import type { RepositoryContentEntry } from '~/utils/api'
import { formatEntrySize, repoBlobPath, repoTreePath } from '~/utils/repoBrowse'

const props = defineProps<{
  owner: string
  repo: string
  refName: string
  entries: RepositoryContentEntry[]
}>()

const { t } = useI18n()

function entryHref(entry: RepositoryContentEntry): string {
  if (entry.type === 'tree') {
    return repoTreePath(props.owner, props.repo, props.refName, entry.path)
  }
  return repoBlobPath(props.owner, props.repo, props.refName, entry.path)
}

function entryIcon(entry: RepositoryContentEntry): string {
  return entry.type === 'tree' ? 'i-lucide-folder' : 'i-lucide-file'
}
</script>

<template>
  <div class="overflow-x-auto rounded-md border border-[var(--ogb-border)]">
    <table class="w-full text-sm">
      <thead class="border-b border-[var(--ogb-border)] bg-[var(--ogb-bg)] text-left text-[var(--ogb-text-muted)]">
        <tr>
          <th class="px-4 py-2 font-medium">
            {{ t('repo.browse.nameColumn') }}
          </th>
          <th class="px-4 py-2 font-medium text-right">
            {{ t('repo.browse.sizeColumn') }}
          </th>
        </tr>
      </thead>
      <tbody>
        <tr
          v-if="entries.length === 0"
          class="border-b border-[var(--ogb-border)]"
        >
          <td
            colspan="2"
            class="px-4 py-6 text-center text-[var(--ogb-text-muted)]"
          >
            {{ t('repo.browse.emptyDirectory') }}
          </td>
        </tr>
        <tr
          v-for="entry in entries"
          :key="entry.path"
          class="border-b border-[var(--ogb-border)] last:border-b-0 hover:bg-[var(--ogb-bg)]"
        >
          <td class="px-4 py-2">
            <NuxtLink
              :to="entryHref(entry)"
              class="inline-flex items-center gap-2 font-mono text-[var(--ogb-accent)] hover:underline"
            >
              <UIcon
                :name="entryIcon(entry)"
                class="size-4 shrink-0 text-[var(--ogb-text-muted)]"
              />
              {{ entry.name }}
            </NuxtLink>
          </td>
          <td class="px-4 py-2 text-right font-mono text-[var(--ogb-text-muted)]">
            {{ entry.type === 'tree' ? '—' : formatEntrySize(entry.size) }}
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>
