<script setup lang="ts">
import type { AdminStatusOutageWindowDto } from '~/utils/api'
import { componentGroupKey } from '~/utils/publicStatus'

const props = defineProps<{
  windows: AdminStatusOutageWindowDto[]
  mutatingId?: string | null
}>()

const emit = defineEmits<{
  suppress: [windowId: string]
  unsuppress: [windowId: string]
  'save-annotation': [windowId: string, annotation: string | null]
}>()

const { t } = useI18n()

const drafts = ref<Record<string, string>>({})

function draftFor(window: AdminStatusOutageWindowDto): string {
  if (!(window.id in drafts.value)) {
    drafts.value[window.id] = window.annotation ?? ''
  }
  return drafts.value[window.id] ?? ''
}

function updateDraft(windowId: string, value: string) {
  drafts.value[windowId] = value
}

function headline(window: AdminStatusOutageWindowDto): string {
  const groupLabel = t(`status.groups.${componentGroupKey(window.group)}`)
  return window.instanceId ? `${window.displayName} (${groupLabel})` : window.displayName
}

function formatRange(window: AdminStatusOutageWindowDto): string {
  const start = new Date(window.startedAt).toLocaleString()
  if (!window.endedAt) {
    return t('admin.status.windows.openSince', { start })
  }
  const end = new Date(window.endedAt).toLocaleString()
  return t('admin.status.windows.closedRange', { start, end })
}

function saveAnnotation(window: AdminStatusOutageWindowDto) {
  const raw = draftFor(window).trim()
  emit('save-annotation', window.id, raw.length ? raw : null)
}
</script>

<template>
  <div
    class="space-y-3"
    data-testid="admin-outage-window-list"
  >
    <p
      v-if="!props.windows.length"
      class="text-sm text-[var(--ogb-text-muted)]"
    >
      {{ t('admin.status.windows.empty') }}
    </p>

    <UCard
      v-for="window in props.windows"
      :key="window.id"
      :data-testid="`admin-outage-window-${window.id}`"
    >
      <div class="flex flex-wrap items-start justify-between gap-3">
        <div class="min-w-0 space-y-1">
          <div class="flex flex-wrap items-center gap-2">
            <span class="font-medium">{{ headline(window) }}</span>
            <UBadge
              v-if="!window.isOpen"
              color="neutral"
              variant="subtle"
            >
              {{ t('admin.status.windows.closed') }}
            </UBadge>
            <UBadge
              v-else
              color="error"
              variant="subtle"
            >
              {{ t('admin.status.windows.open') }}
            </UBadge>
            <UBadge
              v-if="window.isPartial"
              color="warning"
              variant="subtle"
            >
              {{ t('admin.status.windows.partial') }}
            </UBadge>
            <UBadge
              v-if="window.suppressed"
              color="neutral"
              variant="outline"
            >
              {{ t('admin.status.windows.suppressed') }}
            </UBadge>
          </div>
          <p class="text-xs text-[var(--ogb-text-muted)]">
            {{ formatRange(window) }}
          </p>
          <p
            v-if="window.durationMinutes != null"
            class="text-xs text-[var(--ogb-text-muted)]"
          >
            {{ t('admin.status.windows.duration', { minutes: Math.round(window.durationMinutes) }) }}
          </p>
        </div>

        <UButton
          size="sm"
          color="neutral"
          :variant="window.suppressed ? 'solid' : 'outline'"
          :loading="props.mutatingId === window.id"
          @click="window.suppressed ? emit('unsuppress', window.id) : emit('suppress', window.id)"
        >
          {{ window.suppressed ? t('admin.status.windows.unsuppress') : t('admin.status.windows.suppress') }}
        </UButton>
      </div>

      <UFormField
        class="mt-3"
        :label="t('admin.status.windows.annotationLabel')"
      >
        <div class="flex flex-wrap items-start gap-2">
          <UTextarea
            :model-value="draftFor(window)"
            :rows="2"
            maxlength="2000"
            class="min-w-[16rem] flex-1"
            :placeholder="t('admin.status.windows.annotationPlaceholder')"
            @update:model-value="(value) => updateDraft(window.id, String(value ?? ''))"
          />
          <UButton
            size="sm"
            variant="outline"
            color="neutral"
            @click="saveAnnotation(window)"
          >
            {{ t('admin.status.windows.saveAnnotation') }}
          </UButton>
        </div>
      </UFormField>
    </UCard>
  </div>
</template>
