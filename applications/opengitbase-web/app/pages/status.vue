<script setup lang="ts">
import type { PublicStatusHistory, PublicStatusOutageWindowDto, PublicStatusSnapshot } from '~/utils/publicStatus'
import { incidentSeverityKey } from '~/utils/publicStatus'

const { t } = useI18n()
const api = useApi()
const auth = useAuth()

useHead({ title: t('status.title') })

const defaultWindowsDays = 7
const windowsDays = ref(defaultWindowsDays)
const snapshot = ref<PublicStatusSnapshot | null>(null)
const history = ref<PublicStatusHistory | null>(null)
const windows = ref<PublicStatusOutageWindowDto[]>([])
const windowsLoading = ref(false)
const loading = ref(true)
const error = ref<string | null>(null)

const incidentColor = computed(() => {
  const incident = snapshot.value?.incident
  if (!incident) return 'neutral'
  const key = incidentSeverityKey(incident.severity)
  if (key === 'outage') return 'error'
  if (key === 'warning') return 'warning'
  return 'info'
})

async function refreshWindows() {
  windowsLoading.value = true
  const windowsResult = await api.status.getWindows(windowsDays.value)
  if (!windowsResult.error) {
    windows.value = windowsResult.data ?? []
  }
  windowsLoading.value = false
}

async function refresh() {
  error.value = null
  const [statusResult, historyResult] = await Promise.all([
    api.status.get(),
    api.status.getHistory(90),
    refreshWindows(),
  ])

  if (statusResult.error) {
    error.value = statusResult.error
  }
  else {
    snapshot.value = statusResult.data
  }

  if (!historyResult.error) {
    history.value = historyResult.data
  }

  loading.value = false
}

function onWindowsDaysChange(days: number) {
  windowsDays.value = days
  void refreshWindows()
}

onMounted(() => {
  void refresh()
  const timer = window.setInterval(() => {
    void refresh()
  }, 30_000)
  onUnmounted(() => window.clearInterval(timer))
})
</script>

<template>
  <div class="mx-auto max-w-5xl space-y-6">
    <div class="flex flex-wrap items-start justify-between gap-4">
      <div>
        <h1 class="text-2xl font-semibold">
          {{ t('status.title') }}
        </h1>
        <p class="mt-1 text-sm text-[var(--ogb-text-muted)]">
          {{ t('status.subtitle') }}
        </p>
      </div>
      <div
        v-if="auth.isAdmin"
        class="flex flex-wrap gap-2"
      >
        <UButton
          to="/admin/status"
          variant="outline"
          color="neutral"
          icon="i-lucide-megaphone"
        >
          {{ t('status.adminIncident') }}
        </UButton>
        <UButton
          to="/admin/storage"
          variant="outline"
          color="neutral"
          icon="i-lucide-server"
        >
          {{ t('status.adminStorage') }}
        </UButton>
      </div>
    </div>

    <UAlert
      v-if="error"
      color="error"
      variant="subtle"
      :title="t('status.loadError')"
      :description="error"
    />

    <UAlert
      v-if="snapshot?.incident"
      :color="incidentColor"
      variant="subtle"
      :title="t(`status.incident.${incidentSeverityKey(snapshot.incident.severity)}`)"
      :description="snapshot.incident.message"
    />

    <UCard v-if="loading">
      <p class="text-sm text-[var(--ogb-text-muted)]">
        {{ t('common.loading') }}
      </p>
    </UCard>

    <template v-else-if="snapshot">
      <UCard>
        <div class="flex flex-wrap items-center justify-between gap-4">
          <div>
            <p class="text-sm text-[var(--ogb-text-muted)]">
              {{ t('status.overall') }}
            </p>
            <div class="mt-2">
              <StatusHealthBadge :status="snapshot.overallStatus" />
            </div>
          </div>
          <p class="text-sm text-[var(--ogb-text-muted)]">
            {{ t('status.lastUpdated', { time: new Date(snapshot.checkedAt).toLocaleString() }) }}
          </p>
        </div>
      </UCard>

      <div class="space-y-4">
        <StatusGroupPanel
          v-for="group in snapshot.groups"
          :key="group.group"
          :group="group"
        />
      </div>

      <StatusOutageTimeline
        :windows="windows"
        :groups="snapshot.groups"
        :days="windowsDays"
        :loading="windowsLoading"
        @update:days="onWindowsDaysChange"
      />

      <StatusHistoryCharts :history="history" />
    </template>
  </div>
</template>
