<script setup lang="ts">
import type { StatusGroupSnapshot } from '~/utils/publicStatus'
import { componentGroupKey, healthStatusLabel } from '~/utils/publicStatus'

const props = defineProps<{
  group: StatusGroupSnapshot
}>()

const { t } = useI18n()

const groupKey = computed(() => componentGroupKey(props.group.group))
const expanded = ref(healthStatusLabel(props.group.status) !== 'healthy')

function formatTime(value?: string | null) {
  if (!value) return t('status.notAvailable')
  return new Date(value).toLocaleString()
}
</script>

<template>
  <UCard>
    <button
      type="button"
      class="flex w-full items-center justify-between gap-3 text-left"
      @click="expanded = !expanded"
    >
      <div class="flex min-w-0 items-center gap-3">
        <UIcon
          :name="expanded ? 'i-lucide-chevron-down' : 'i-lucide-chevron-right'"
          class="size-4 shrink-0 text-[var(--ogb-text-muted)]"
        />
        <div>
          <h2 class="font-semibold">
            {{ t(`status.groups.${groupKey}`) }}
          </h2>
          <p class="text-xs text-[var(--ogb-text-muted)]">
            {{ t('status.instanceCount', { count: group.instances.length }) }}
          </p>
        </div>
      </div>
      <StatusHealthBadge :status="group.status" />
    </button>

    <div
      v-if="expanded"
      class="mt-4 overflow-x-auto"
    >
      <table class="min-w-full text-sm">
        <thead class="text-left text-[var(--ogb-text-muted)]">
          <tr>
            <th class="pb-2 pr-4 font-medium">
              {{ t('status.columns.instance') }}
            </th>
            <th class="pb-2 pr-4 font-medium">
              {{ t('status.columns.status') }}
            </th>
            <th class="pb-2 pr-4 font-medium">
              {{ t('status.columns.lastChecked') }}
            </th>
            <th class="pb-2 pr-4 font-medium">
              {{ t('status.columns.responseTime') }}
            </th>
            <th class="pb-2 pr-4 font-medium">
              {{ t('status.columns.lastSeen') }}
            </th>
            <th class="pb-2 font-medium">
              {{ t('status.columns.message') }}
            </th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="instance in group.instances"
            :key="instance.instanceId"
            class="border-t"
            style="border-color: var(--ogb-border);"
          >
            <td class="py-2 pr-4 font-mono text-xs">
              {{ instance.instanceId }}
            </td>
            <td class="py-2 pr-4">
              <StatusHealthBadge :status="instance.status" />
            </td>
            <td class="py-2 pr-4 whitespace-nowrap">
              {{ formatTime(instance.lastCheckedAt) }}
            </td>
            <td class="py-2 pr-4 whitespace-nowrap">
              {{ instance.responseTimeMs != null ? `${instance.responseTimeMs}ms` : t('status.notAvailable') }}
            </td>
            <td class="py-2 pr-4 whitespace-nowrap">
              {{ formatTime(instance.lastSeenAt) }}
            </td>
            <td class="py-2 text-[var(--ogb-text-muted)]">
              {{ instance.message || t('status.noMessage') }}
            </td>
          </tr>
          <tr v-if="!group.instances.length">
            <td
              colspan="6"
              class="py-3 text-[var(--ogb-text-muted)]"
            >
              {{ t('status.noInstances') }}
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </UCard>
</template>
