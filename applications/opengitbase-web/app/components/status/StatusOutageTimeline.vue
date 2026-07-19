<script setup lang="ts">
import type { PublicStatusOutageWindowDto, StatusGroupSnapshot } from '~/utils/publicStatus'

const props = defineProps<{
  windows: PublicStatusOutageWindowDto[]
  groups?: StatusGroupSnapshot[]
  days?: number
}>()

const { t } = useI18n()

const timezoneMode = ref<'utc' | 'local'>('utc')
const expandedIds = ref<Set<string>>(new Set())

const primaryWindows = computed(() => props.windows.filter(window => !window.isPartial))
const partialWindows = computed(() => props.windows.filter(window => window.isPartial))
const lookbackDays = computed(() => props.days ?? 7)

function setTimezoneMode(mode: 'utc' | 'local') {
  timezoneMode.value = mode
}

function toggleExpand(windowId: string) {
  const next = new Set(expandedIds.value)
  if (next.has(windowId)) {
    next.delete(windowId)
  }
  else {
    next.add(windowId)
  }
  expandedIds.value = next
}

function formatTimestamp(value: string): string {
  const date = new Date(value)
  if (timezoneMode.value === 'utc') {
    const formatted = date.toLocaleString('en-US', {
      timeZone: 'UTC',
      year: 'numeric',
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
      hour12: false,
    })
    return `${formatted} UTC`
  }
  return date.toLocaleString()
}

function headline(window: PublicStatusOutageWindowDto): string {
  if (window.isOpen) {
    return t('status.timeline.downSince', {
      name: window.displayName,
      start: formatTimestamp(window.startedAt),
    })
  }
  return t('status.timeline.downRange', {
    name: window.displayName,
    start: formatTimestamp(window.startedAt),
    end: formatTimestamp(window.endedAt ?? window.startedAt),
  })
}

function liveUnhealthyInstances(window: PublicStatusOutageWindowDto) {
  const group = props.groups?.find(candidate => candidate.group === window.group)
  if (!group) return []
  return group.instances.filter(instance => instance.status === 2)
}

function canExpand(window: PublicStatusOutageWindowDto): boolean {
  return window.isOpen && window.scope === 0 && liveUnhealthyInstances(window).length > 0
}
</script>

<template>
  <UCard data-testid="status-outage-timeline">
    <div class="space-y-4">
      <div class="flex flex-wrap items-end justify-between gap-3">
        <div>
          <h2 class="text-lg font-semibold">
            {{ t('status.timeline.title') }}
          </h2>
          <p class="text-sm text-[var(--ogb-text-muted)]">
            {{ t('status.timeline.subtitle', { days: lookbackDays }) }}
          </p>
        </div>
        <div class="flex gap-2">
          <UButton
            size="xs"
            color="neutral"
            :variant="timezoneMode === 'utc' ? 'solid' : 'outline'"
            @click="setTimezoneMode('utc')"
          >
            {{ t('status.timeline.utc') }}
          </UButton>
          <UButton
            size="xs"
            color="neutral"
            :variant="timezoneMode === 'local' ? 'solid' : 'outline'"
            @click="setTimezoneMode('local')"
          >
            {{ t('status.timeline.local') }}
          </UButton>
        </div>
      </div>

      <p
        v-if="!primaryWindows.length"
        class="text-sm text-[var(--ogb-text-muted)]"
        data-testid="status-outage-timeline-empty"
      >
        {{ t('status.timeline.empty', { days: lookbackDays }) }}
      </p>

      <div
        v-else
        class="space-y-3"
      >
        <div
          v-for="window in primaryWindows"
          :key="window.id"
          class="rounded-lg border border-[var(--ogb-border)] p-3"
          :data-testid="`status-outage-window-${window.id}`"
        >
          <div class="flex flex-wrap items-start justify-between gap-3">
            <div class="min-w-0 space-y-1">
              <div class="flex flex-wrap items-center gap-2">
                <span class="font-medium">{{ headline(window) }}</span>
                <UBadge
                  v-if="window.isOpen"
                  color="error"
                  variant="subtle"
                >
                  {{ t('status.timeline.open') }}
                </UBadge>
              </div>
              <p
                v-if="window.durationMinutes != null"
                class="text-xs text-[var(--ogb-text-muted)]"
              >
                {{ t('status.timeline.duration', { minutes: Math.round(window.durationMinutes) }) }}
              </p>
              <p
                v-if="window.annotation"
                class="text-xs italic text-[var(--ogb-text-muted)]"
              >
                {{ t('status.timeline.annotationPrefix') }} {{ window.annotation }}
              </p>
            </div>

            <UButton
              v-if="canExpand(window)"
              size="xs"
              variant="ghost"
              color="neutral"
              @click="toggleExpand(window.id)"
            >
              {{ expandedIds.has(window.id) ? t('status.timeline.collapse') : t('status.timeline.expand') }}
            </UButton>
          </div>

          <ul
            v-if="canExpand(window) && expandedIds.has(window.id)"
            class="mt-3 space-y-1 border-t border-[var(--ogb-border)] pt-2 text-xs text-[var(--ogb-text-muted)]"
          >
            <li
              v-for="instance in liveUnhealthyInstances(window)"
              :key="instance.instanceId"
              class="font-mono"
            >
              {{ instance.instanceId }}
            </li>
          </ul>
        </div>
      </div>

      <div
        v-if="partialWindows.length"
        class="space-y-3 border-t border-[var(--ogb-border)] pt-4"
        data-testid="status-outage-timeline-partial"
      >
        <div>
          <h3 class="text-sm font-semibold">
            {{ t('status.timeline.partialTitle') }}
          </h3>
          <p class="text-xs text-[var(--ogb-text-muted)]">
            {{ t('status.timeline.partialSubtitle') }}
          </p>
        </div>

        <div
          v-for="window in partialWindows"
          :key="window.id"
          class="rounded-lg border border-[var(--ogb-border)] p-3"
          :data-testid="`status-outage-window-${window.id}`"
        >
          <div class="flex flex-wrap items-center gap-2">
            <span class="font-medium">{{ headline(window) }}</span>
            <UBadge
              v-if="window.isOpen"
              color="error"
              variant="subtle"
            >
              {{ t('status.timeline.open') }}
            </UBadge>
          </div>
          <p
            v-if="window.durationMinutes != null"
            class="text-xs text-[var(--ogb-text-muted)]"
          >
            {{ t('status.timeline.duration', { minutes: Math.round(window.durationMinutes) }) }}
          </p>
          <p
            v-if="window.annotation"
            class="text-xs italic text-[var(--ogb-text-muted)]"
          >
            {{ t('status.timeline.annotationPrefix') }} {{ window.annotation }}
          </p>
        </div>
      </div>
    </div>
  </UCard>
</template>
