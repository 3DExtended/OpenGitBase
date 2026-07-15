<script setup lang="ts">
import type { PublicStatusHistory, PublicStatusHistoryDay } from '~/utils/publicStatus'
import { componentGroupKey } from '~/utils/publicStatus'

const props = defineProps<{
  history: PublicStatusHistory | null
}>()

const { t } = useI18n()

const enabledGroups = ref<Record<string, boolean>>({
  website: false,
  api: false,
  git: false,
  storage: false,
  dataStores: false,
  messageBus: false,
})

const overallSeries = computed(() => props.history?.overall ?? [])
const stateMixSeries = computed(() => props.history?.overallStateMix ?? [])
const groupSeries = computed(() => props.history?.groups ?? [])

const dayCount = computed(() => overallSeries.value.length)
const latestOverall = computed(() => overallSeries.value.at(-1) ?? null)

const chartWidth = 640
const chartHeight = 160
const chartPadding = { top: 8, right: 8, bottom: 24, left: 36 }
const plotWidth = chartWidth - chartPadding.left - chartPadding.right
const plotHeight = chartHeight - chartPadding.top - chartPadding.bottom

function toggleGroup(key: string) {
  enabledGroups.value[key] = !enabledGroups.value[key]
}

function yForUptime(uptimePercent: number) {
  return chartPadding.top + plotHeight - (uptimePercent / 100) * plotHeight
}

function xForIndex(index: number, count: number) {
  if (count <= 1) {
    return chartPadding.left + plotWidth / 2
  }
  return chartPadding.left + (index / (count - 1)) * plotWidth
}

function buildLinePath(days: PublicStatusHistoryDay[]) {
  if (!days.length) {
    return ''
  }

  if (days.length === 1) {
    const y = yForUptime(days[0]!.uptimePercent)
    return `M ${chartPadding.left} ${y} L ${chartPadding.left + plotWidth} ${y}`
  }

  return days
    .map((day, index) => {
      const x = xForIndex(index, days.length)
      const y = yForUptime(day.uptimePercent)
      return `${index === 0 ? 'M' : 'L'} ${x} ${y}`
    })
    .join(' ')
}

function buildPointMarkers(days: PublicStatusHistoryDay[]) {
  return days.map((day, index) => ({
    x: xForIndex(index, days.length),
    y: yForUptime(day.uptimePercent),
    label: day.date,
    uptimePercent: day.uptimePercent,
  }))
}

const overallPath = computed(() => buildLinePath(overallSeries.value))
const overallMarkers = computed(() => buildPointMarkers(overallSeries.value))

const activeOverlayPaths = computed(() =>
  groupSeries.value
    .filter(series => enabledGroups.value[componentGroupKey(series.group)])
    .map(series => ({
      key: componentGroupKey(series.group),
      path: buildLinePath(series.days),
      markers: buildPointMarkers(series.days),
    })),
)

const gridLines = [0, 50, 100]

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

function formatPercent(value: number) {
  return `${value.toFixed(1)}%`
}
</script>

<template>
  <div class="space-y-6">
    <UCard>
      <div class="space-y-4">
        <div class="flex flex-wrap items-end justify-between gap-3">
          <div>
            <h2 class="text-lg font-semibold">
              {{ t('status.charts.uptimeTitle') }}
            </h2>
            <p class="text-sm text-[var(--ogb-text-muted)]">
              {{ t('status.charts.uptimeSubtitle') }}
            </p>
          </div>
          <p
            v-if="latestOverall"
            class="text-sm font-medium tabular-nums"
          >
            {{ t('status.charts.latestUptime', { value: formatPercent(latestOverall.uptimePercent) }) }}
          </p>
        </div>

        <p
          v-if="dayCount > 0 && dayCount < 90"
          class="text-xs text-[var(--ogb-text-muted)]"
        >
          {{ t('status.charts.partialData', { count: dayCount }) }}
        </p>

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
          class="h-44 w-full text-[var(--ogb-accent)]"
          role="img"
          :aria-label="t('status.charts.uptimeTitle')"
        >
          <g class="text-[var(--ogb-text-muted)]">
            <line
              v-for="tick in gridLines"
              :key="tick"
              :x1="chartPadding.left"
              :x2="chartPadding.left + plotWidth"
              :y1="yForUptime(tick)"
              :y2="yForUptime(tick)"
              stroke="currentColor"
              stroke-opacity="0.15"
            />
            <text
              v-for="tick in gridLines"
              :key="`label-${tick}`"
              :x="chartPadding.left - 6"
              :y="yForUptime(tick) + 4"
              text-anchor="end"
              font-size="10"
              fill="currentColor"
            >
              {{ tick }}
            </text>
          </g>

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

          <g>
            <circle
              v-for="(point, index) in overallMarkers"
              :key="`overall-${index}`"
              :cx="point.x"
              :cy="point.y"
              r="3.5"
              fill="currentColor"
            />
          </g>

          <text
            v-for="(point, index) in overallMarkers"
            :key="`date-${index}`"
            :x="point.x"
            :y="chartHeight - 6"
            text-anchor="middle"
            font-size="10"
            fill="var(--ogb-text-muted)"
          >
            {{ point.label }}
          </text>
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
          class="space-y-2"
        >
          <div
            class="flex h-32 items-end gap-1"
            role="img"
            :aria-label="t('status.charts.stateMixTitle')"
          >
            <div
              v-for="day in stateMixSeries"
              :key="day.date"
              class="flex min-h-24 min-w-8 flex-1 flex-col justify-end"
              :title="`${day.date}: ${formatPercent(day.uptimePercent)} uptime`"
            >
              <div class="flex h-full min-h-20 flex-col overflow-hidden rounded-sm border border-[var(--ogb-border)]">
                <div
                  class="bg-red-500/80"
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
              <p class="mt-1 truncate text-center text-[10px] text-[var(--ogb-text-muted)]">
                {{ day.date }}
              </p>
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
