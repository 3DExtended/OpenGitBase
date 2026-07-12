<script setup lang="ts">
import type { StorageNodeEnrollmentDto } from '~/utils/api'

defineProps<{
  enrollments: StorageNodeEnrollmentDto[]
  createNodeId: string
  createMaxGiB: number
  createHostingScope: number
  createLoading?: boolean
  bootstrapCommand?: string | null
  bootstrapDownloadFilename?: string
}>()

const emit = defineEmits<{
  'update:createNodeId': [value: string]
  'update:createMaxGiB': [value: number]
  'update:createHostingScope': [value: number]
  createEnrollment: []
  downloadBootstrap: []
}>()

const { t } = useI18n()

const hostingScopeOptions = [
  { label: t('org.storage.hostingScope.ownOrgOnly'), value: 0 },
  { label: t('org.storage.hostingScope.crossOrgAllowed'), value: 1 },
]
</script>

<template>
  <UCard>
    <template #header>
      <h2 class="font-semibold">
        {{ t('org.storage.enrollmentsTitle') }}
      </h2>
    </template>
    <form
      class="grid gap-3 md:grid-cols-2"
      @submit.prevent="emit('createEnrollment')"
    >
      <UFormField
        :label="t('org.storage.nodeIdLabel')"
        required
      >
        <UInput
          :model-value="createNodeId"
          @update:model-value="emit('update:createNodeId', String($event))"
        />
      </UFormField>
      <UFormField
        :label="t('org.storage.maxCapacityGiBLabel')"
        required
      >
        <UInput
          :model-value="createMaxGiB"
          type="number"
          min="1"
          @update:model-value="emit('update:createMaxGiB', Number($event))"
        />
      </UFormField>
      <UFormField
        :label="t('org.storage.hostingScopeLabel')"
        class="md:col-span-2"
      >
        <USelect
          :model-value="createHostingScope"
          :items="hostingScopeOptions"
          @update:model-value="emit('update:createHostingScope', Number($event))"
        />
      </UFormField>
      <div class="flex items-end md:col-span-2">
        <UButton
          type="submit"
          :loading="createLoading"
        >
          {{ t('org.storage.createEnrollment') }}
        </UButton>
      </div>
    </form>

    <UAlert
      v-if="bootstrapCommand"
      class="mt-4"
      color="warning"
      variant="subtle"
      :title="t('org.storage.tokenTitle')"
    >
      <template #description>
        <div class="space-y-3">
          <p class="text-sm text-[var(--ogb-text-muted)]">
            {{ t('org.storage.bootstrapHint') }}
          </p>
          <pre class="overflow-x-auto rounded bg-[var(--ogb-surface-muted)] p-2 text-xs"><code>{{ bootstrapCommand }}</code></pre>
          <UButton
            size="sm"
            variant="soft"
            @click="emit('downloadBootstrap')"
          >
            {{ t('org.storage.downloadBootstrap') }}
          </UButton>
          <p class="text-xs text-[var(--ogb-text-muted)]">
            {{ t('org.storage.tokenOnce') }}
          </p>
        </div>
      </template>
    </UAlert>

    <div class="mt-4 space-y-2">
      <div
        v-if="!enrollments.length"
        class="text-sm text-[var(--ogb-text-muted)]"
      >
        {{ t('org.storage.enrollmentsEmpty') }}
      </div>
      <UCard
        v-for="enrollment in enrollments"
        :key="enrollment.id"
        class="bg-[var(--ogb-bg)]"
      >
        <div class="flex items-center gap-2">
          <span class="font-medium">{{ enrollment.nodeId }}</span>
          <UBadge
            :color="enrollment.consumedAt ? 'neutral' : 'success'"
            variant="subtle"
          >
            {{ enrollment.consumedAt ? t('org.storage.consumed') : t('org.storage.active') }}
          </UBadge>
        </div>
        <div class="mt-1 text-xs text-[var(--ogb-text-muted)]">
          {{ t('org.storage.expiresAt', { date: enrollment.expiresAt }) }}
        </div>
      </UCard>
    </div>
  </UCard>
</template>
