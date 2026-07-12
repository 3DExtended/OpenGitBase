<script setup lang="ts">
import type {
  BaseImageCatalogEntryDto,
  DependencyInstallAnalyticsDto,
  DependencyPromotionRequestDto,
} from '~/utils/api'

definePageMeta({ middleware: 'admin' })

const { t } = useI18n()
const api = useApi()

useHead({ title: t('admin.ci.title') })

const loading = ref(true)
const error = ref<string | null>(null)
const baseImages = ref<BaseImageCatalogEntryDto[]>([])
const analytics = ref<DependencyInstallAnalyticsDto[]>([])
const promotions = ref<DependencyPromotionRequestDto[]>([])

const createSlug = ref('alpine')
const createVersion = ref('3.20')
const createHash = ref('')
const createOci = ref('docker.io/library/alpine:3.20')
const createLoading = ref(false)

async function refreshAll() {
  loading.value = true
  error.value = null
  try {
    const [images, stats, promo] = await Promise.all([
      api.admin.ci.listBaseImages(),
      api.admin.ci.listDependencyAnalytics(),
      api.admin.ci.listPromotions(),
    ])
    baseImages.value = images.data ?? []
    analytics.value = stats.data ?? []
    promotions.value = promo.data ?? []
  }
  catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to load admin CI console.'
  }
  finally {
    loading.value = false
  }
}

async function createBaseImage() {
  createLoading.value = true
  try {
    await api.admin.ci.createBaseImage({
      slug: createSlug.value,
      versionLabel: createVersion.value,
      artifactUri: createHash.value,
      contentHash: createHash.value,
      ociProvenance: createOci.value,
    })
    await refreshAll()
  }
  catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to create base image entry.'
  }
  finally {
    createLoading.value = false
  }
}

async function requestPromotion(recipeKey: string) {
  await api.admin.ci.requestPromotion({ recipeKey })
  await refreshAll()
}

onMounted(() => {
  void refreshAll()
})
</script>

<template>
  <div class="mx-auto max-w-6xl space-y-6">
    <div>
      <h1 class="text-2xl font-semibold">
        {{ t('admin.ci.title') }}
      </h1>
      <p class="text-sm text-[var(--ogb-text-muted)]">
        {{ t('admin.ci.description') }}
      </p>
    </div>

    <UAlert
      v-if="error"
      color="error"
      variant="soft"
      :title="error"
    />

    <UCard>
      <template #header>
        <h2 class="font-semibold">
          {{ t('admin.ci.baseImagesTitle') }}
        </h2>
      </template>
      <div
        v-if="loading"
        class="text-sm text-[var(--ogb-text-muted)]"
      >
        {{ t('common.loading') }}
      </div>
      <div
        v-else-if="!baseImages.length"
        class="text-sm text-[var(--ogb-text-muted)]"
      >
        {{ t('admin.ci.baseImagesEmpty') }}
      </div>
      <ul
        v-else
        class="space-y-2"
      >
        <li
          v-for="entry in baseImages"
          :key="entry.id"
          class="rounded border p-3 font-mono text-sm"
          style="border-color: var(--ogb-border);"
        >
          {{ entry.slug }} · {{ entry.versionLabel }} · {{ entry.contentHash }}
        </li>
      </ul>
      <form
        class="mt-4 grid gap-3 md:grid-cols-2"
        @submit.prevent="createBaseImage"
      >
        <UInput
          v-model="createSlug"
          :label="t('admin.ci.slugLabel')"
        />
        <UInput
          v-model="createVersion"
          :label="t('admin.ci.versionLabel')"
        />
        <UInput
          v-model="createHash"
          :label="t('admin.ci.hashLabel')"
        />
        <UInput
          v-model="createOci"
          :label="t('admin.ci.ociLabel')"
        />
        <UButton
          type="submit"
          :loading="createLoading"
        >
          {{ t('admin.ci.createBaseImage') }}
        </UButton>
      </form>
    </UCard>

    <UCard>
      <template #header>
        <h2 class="font-semibold">
          {{ t('admin.ci.promotionTitle') }}
        </h2>
      </template>
      <div
        v-if="!analytics.length"
        class="text-sm text-[var(--ogb-text-muted)]"
      >
        {{ t('admin.ci.promotionEmpty') }}
      </div>
      <div
        v-else
        class="space-y-3"
      >
        <div
          v-for="item in analytics"
          :key="item.recipeKey"
          class="rounded border p-3"
          style="border-color: var(--ogb-border);"
        >
          <div class="flex flex-wrap items-center justify-between gap-2">
            <p class="font-mono text-sm">
              {{ item.recipeKey }}
            </p>
            <UButton
              v-if="item.promotionEligible"
              size="xs"
              @click="requestPromotion(item.recipeKey)"
            >
              {{ t('admin.ci.requestPromotion') }}
            </UButton>
          </div>
          <p class="text-xs text-[var(--ogb-text-muted)]">
            {{ item.installCount }} installs · {{ Math.round(item.successRate * 100) }}% success · median {{ item.medianDurationMs }}ms
          </p>
          <p
            v-if="item.promotionBlockedReason"
            class="text-xs text-[var(--ogb-text-muted)]"
          >
            {{ item.promotionBlockedReason }}
          </p>
        </div>
      </div>
      <ul
        v-if="promotions.length"
        class="mt-4 space-y-2 border-t pt-4"
        style="border-color: var(--ogb-border);"
      >
        <li
          v-for="promotion in promotions"
          :key="promotion.id"
          class="font-mono text-xs text-[var(--ogb-text-muted)]"
        >
          {{ promotion.recipeKey }} · status={{ promotion.status }} · {{ promotion.createdAt }}
        </li>
      </ul>
    </UCard>
  </div>
</template>
