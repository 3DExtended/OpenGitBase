<script setup lang="ts">
import type { PublicStatusHistory, PublicStatusHistoryDay } from '~/utils/publicStatus'
import { componentGroupKey } from '~/utils/publicStatus'

const props = defineProps<{
  history: PublicStatusHistory | null
}>()

const { t } = useI18n()

const enabledGroups = ref<Record<string, boolean>>({
  website: true,
  api: false,
  git: false,
  storage: false,
  dataStores: false,
})

const overallSeries = computed(() => props.history?.overall ?? [])
const stateMixSeries = computed(() => props.history?.overallStateMix ?? [])

const groupSeries = computed(() => props.history?.groups ?? [])

function toggleGroup(key: string) {
  enabledGroups.value[key] = !enabledGroups.value[key]
}

function buildLinePath(days: PublicStatusHistoryDay[], width: number, height: number) {
  if (!days.length) return ''
  const maxY = 100
  const stepX = days.length === 1 ? 0 : width / (days.length - 1)
  return days
    .map((day, index) => {
      const x = index * stepX
      const y = height - (day.uptimePercent / maxY) * height
      return `${index === 0 ? 'M' : 'L'} ${x} ${y}`
    })
    .join(' ')
}

const chartWidth = 640
const chartHeight = 160
const overallPath = computed(() => buildLinePath(overallSeries.value, chartWidth, chartHeight))

function overlayPaths() {
  return groupSeries.value
    .filter(series => enabledGroups.value[componentGroupKey(series.group)])
    .map(series => ({
      key: componentGroupKey(series.group),
      path: buildLinePath(series.days, chartWidth, chartHeight),
    }))
}

const activeOverlayPaths = computed(() => overlayPaths())

function barSegments(day: PublicStatusHistoryDay) {
  const total = day.healthyRatio + day.degradedRatio + day.unhealthyRatio
  if (total <= 0) {
    return { healthy: 0, degraded: 0, unhealthy: 1 }
  }
  return {
    healthy: day.healthyRatio / total,
    degraded: day.degradedRatio / total,
    unhealthy: day.unhealthyRatio / total,
  }
}
</script>

<template>
  <div class="space-y-6">
    <UCard>
      <div class="space-y-4">
        <div>
          <h2 class="text-lg font-semibold">
            {{ t('status.charts.uptimeTitle') }}
          </h2>
          <p class="text-sm text-[var(--ogb-text-muted)]">
            {{ t('status.charts.uptimeSubtitle') }}
          </p>
        </div>

        <div class="flex flex-wrap gap-2">
          <UButton
            v-for="series in groupSeries"
            :key="series.group"
            size="xs"
            :variant="enabledGroups[componentGroupKey(series.group)] ? 'solid' : 'outline'"
            color="neutral"
            @click="toggleGroup(componentGroupKey(series.group))"
          >
            {{ t(`status.groups.${componentGroupKey(series.group)}`) }}
          </UButton>
        </div>

        <div
          v-if="!overallSeries.length"
          class="text-sm text-[var(--ogb-text-muted)]"
        >
          {{ t('status.charts.empty') }}
        </div>
        <svg
          v-else
          :viewBox="`0 0 ${chartWidth} ${chartHeight}`"
          class="h-40 w-full text-[var(--ogb-accent)]"
          role="img"
          :aria-label="t('status.charts.uptimeTitle')"
        >
          <path
            :d="overallPath"
            fill="none"
            stroke="currentColor"
            stroke-width="2"
          />
          <path
            v-for="overlay in activeOverlayPaths"
            :key="overlay.key"
            :d="overlay.path"
            fill="none"
            stroke="currentColor"
            stroke-width="1.5"
            opacity="0.55"
          />
        </svg>
      </div>
    </UCard>

    <UCard>
      <div class="space-y-4">
        <div>
          <h2 class="text-lg font-semibold">
            {{ t('status.charts.stateMixTitle') }}
          </h2>
          <p class="text-sm text-[var(--ogb-text-muted)]">
            {{ t('status.charts.stateMixSubtitle') }}
          </p>
        </div>

        <div
          v-if="!stateMixSeries.length"
          class="text-sm text-[var(--ogb-text-muted)]"
        >
          {{ t('status.charts.empty') }}
        </div>
        <div
          v-else
          class="flex h-32 items-end gap-1"
          role="img"
          :aria-label="t('status.charts.stateMixTitle')"
        >
          <div
            v-for="day in stateMixSeries"
            :key="day.date"
            class="flex min-w-0 flex-1 flex-col justify-end"
            :title="day.date"
          >
            <div class="flex h-full flex-col overflow-hidden rounded-sm">
              <div
                class="bg-emerald-500/80"
                :style="{ flexGrow: barSegments(day).unhealthy }"
              />
              <div
                class="bg-amber-500/80"
                :style="{ flexGrow: barSegments(day).degraded }"
              />
              <div
                class="bg-emerald-400/90"
                :style="{ flexGrow: barSegments(day).healthy }"
              />
            </div>
          </div>
        </div>

        <div class="flex flex-wrap gap-4 text-xs text-[var(--ogb-text-muted)]">
          <span class="inline-flex items-center gap-1">
            <span class="size-2 rounded-full bg-emerald-400" /> {{ t('status.health.healthy') }}
          </span>
          <span class="inline-flex items-center gap-1">
            <span class="size-2 rounded-full bg-amber-500" /> {{ t('status.health.degraded') }}
          </span>
          <span class="inline-flex items-center gap-1">
            <span class="size-2 rounded-full bg-red-500" /> {{ t('status.health.unhealthy') }}
          </span>
        </div>
      </div>
    </UCard>
  </div>
</template>
