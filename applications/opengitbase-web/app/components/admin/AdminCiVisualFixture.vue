<script setup lang="ts">
const { t } = useI18n()

const baseImages = [
  { id: '1', slug: 'alpine', versionLabel: '3.20', contentHash: 'abc123' },
]

const analytics = [
  {
    recipeKey: 'linux:apt:docker',
    installCount: 6,
    successRate: 1,
    medianDurationMs: 1200,
    promotionEligible: true,
  },
]
</script>

<template>
  <div class="mx-auto max-w-4xl space-y-4">
    <h1 class="text-2xl font-semibold">
      {{ t('admin.ci.title') }}
    </h1>
    <UCard>
      <template #header>
        {{ t('admin.ci.baseImagesTitle') }}
      </template>
      <ul class="space-y-2 font-mono text-sm">
        <li
          v-for="entry in baseImages"
          :key="entry.id"
        >
          {{ entry.slug }} · {{ entry.versionLabel }} · {{ entry.contentHash }}
        </li>
      </ul>
    </UCard>
    <UCard>
      <template #header>
        {{ t('admin.ci.promotionTitle') }}
      </template>
      <div
        v-for="item in analytics"
        :key="item.recipeKey"
        class="font-mono text-sm"
      >
        {{ item.recipeKey }} · {{ item.installCount }} installs
      </div>
    </UCard>
  </div>
</template>
