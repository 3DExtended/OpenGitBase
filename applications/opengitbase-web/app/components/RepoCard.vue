<script setup lang="ts">
import type { Repository } from '~/utils/api'

defineProps<{
  repo: Repository
  ownerSlug?: string
}>()

const { t } = useI18n()
</script>

<template>
  <UCard
    class="transition-shadow hover:shadow-sm"
    :ui="{ body: 'p-4 sm:p-5' }"
  >
    <div class="flex items-start justify-between gap-3">
      <div class="min-w-0">
        <NuxtLink
          :to="`/${ownerSlug ?? repo.ownerSlug ?? repo.ownerUserId}/${repo.slug}`"
          class="font-medium hover:text-[var(--ogb-accent)]"
        >
          {{ repo.name }}
        </NuxtLink>
        <p class="mt-1 truncate font-mono text-xs text-[var(--ogb-text-muted)]">
          {{ ownerSlug ?? repo.ownerSlug ?? 'owner' }}/{{ repo.slug }}
        </p>
      </div>
      <UBadge
        :color="repo.isPrivate ? 'neutral' : 'success'"
        variant="subtle"
        size="xs"
      >
        {{ repo.isPrivate ? t('repo.visibility.private') : t('repo.visibility.public') }}
      </UBadge>
    </div>
    <p
      v-if="repo.updatedAt"
      class="mt-3 text-xs text-[var(--ogb-text-muted)]"
    >
      <i18n-t keypath="repo.updatedAt">
        <template #date>
          <RelativeTime :iso="repo.updatedAt" />
        </template>
      </i18n-t>
    </p>
  </UCard>
</template>
