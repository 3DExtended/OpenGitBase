<script setup lang="ts">
import { formatStorageBytes } from '~/utils/storageUsage'

export interface RepositoryByteOverrideEligibility {
  eligible: boolean
  reason: string
  currentOverride: number | null
  maxAllowedOverride: number
  orgContributedNodeCount: number
}

const props = defineProps<{
  eligibility: RepositoryByteOverrideEligibility | null
  loading?: boolean
  saving?: boolean
  error?: string | null
  success?: boolean
}>()

const emit = defineEmits<{
  save: [maxBytesOverride: number | null]
}>()

const { t } = useI18n()

const overrideInput = ref('')

watch(
  () => props.eligibility?.currentOverride,
  (value) => {
    overrideInput.value = value == null ? '' : String(value)
  },
  { immediate: true },
)

function formatBytes(bytes: number): string {
  return formatStorageBytes(bytes)
}

function saveOverride() {
  const trimmed = overrideInput.value.trim()
  if (!trimmed) {
    emit('save', null)
    return
  }

  const parsed = Number(trimmed)
  if (!Number.isFinite(parsed) || parsed <= 0) {
    return
  }

  emit('save', Math.trunc(parsed))
}

function clearOverride() {
  overrideInput.value = ''
  emit('save', null)
}
</script>

<template>
  <UCard>
    <template #header>
      <h3 class="text-sm font-semibold">
        {{ t('repo.byteOverride.title') }}
      </h3>
    </template>

    <div
      v-if="loading"
      class="text-sm text-[var(--ogb-text-muted)]"
    >
      {{ t('common.loading') }}
    </div>

    <div
      v-else-if="eligibility"
      class="space-y-4"
    >
      <UAlert
        :color="eligibility.eligible ? 'success' : 'neutral'"
        variant="subtle"
        :title="eligibility.eligible ? t('repo.byteOverride.eligibleTitle') : t('repo.byteOverride.ineligibleTitle')"
        :description="eligibility.reason"
      />

      <dl class="grid gap-3 text-sm sm:grid-cols-2">
        <div>
          <dt class="text-[var(--ogb-text-muted)]">
            {{ t('repo.byteOverride.orgNodesLabel') }}
          </dt>
          <dd>{{ eligibility.orgContributedNodeCount }}</dd>
        </div>
        <div>
          <dt class="text-[var(--ogb-text-muted)]">
            {{ t('repo.byteOverride.maxAllowedLabel') }}
          </dt>
          <dd>{{ formatBytes(eligibility.maxAllowedOverride) }}</dd>
        </div>
        <div>
          <dt class="text-[var(--ogb-text-muted)]">
            {{ t('repo.byteOverride.currentOverrideLabel') }}
          </dt>
          <dd>
            {{
              eligibility.currentOverride == null
                ? t('repo.byteOverride.noOverride')
                : formatBytes(eligibility.currentOverride)
            }}
          </dd>
        </div>
      </dl>

      <UFormField
        :label="t('repo.byteOverride.inputLabel')"
        :help="t('repo.byteOverride.inputHelp')"
      >
        <UInput
          v-model="overrideInput"
          inputmode="numeric"
          :disabled="!eligibility.eligible || saving"
          :placeholder="t('repo.byteOverride.inputPlaceholder')"
        />
      </UFormField>

      <UAlert
        v-if="success"
        color="success"
        variant="subtle"
        :description="t('repo.byteOverride.saved')"
      />
      <UAlert
        v-if="error"
        color="error"
        variant="subtle"
        :description="error"
      />

      <div class="flex flex-wrap gap-2">
        <UButton
          :disabled="!eligibility.eligible"
          :loading="saving"
          @click="saveOverride"
        >
          {{ t('repo.byteOverride.save') }}
        </UButton>
        <UButton
          variant="ghost"
          :disabled="!eligibility.currentOverride || saving"
          @click="clearOverride"
        >
          {{ t('repo.byteOverride.clear') }}
        </UButton>
      </div>
    </div>

    <p
      v-else
      class="text-sm text-[var(--ogb-text-muted)]"
    >
      {{ t('repo.byteOverride.unavailable') }}
    </p>
  </UCard>
</template>
