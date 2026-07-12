<script setup lang="ts">
import type { StorageNodeDto } from '~/utils/api'
import { formatStorageBytes } from '~/utils/storageUsage'

defineProps<{
  nodes: StorageNodeDto[]
  editingNodeId?: string | null
  editMaxGiB?: number
  editHostingScope?: number
  editLoading?: boolean
}>()

const emit = defineEmits<{
  startEdit: [node: StorageNodeDto]
  cancelEdit: []
  'update:editMaxGiB': [value: number]
  'update:editHostingScope': [value: number]
  saveEdit: [nodeId: string]
}>()

const { t } = useI18n()

const hostingScopeOptions = [
  { label: t('org.storage.hostingScope.ownOrgOnly'), value: 0 },
  { label: t('org.storage.hostingScope.crossOrgAllowed'), value: 1 },
]

function hostingScopeLabel(scope: number) {
  return scope === 1
    ? t('org.storage.hostingScope.crossOrgAllowed')
    : t('org.storage.hostingScope.ownOrgOnly')
}

function formatBytes(value: number) {
  if (value <= 0) return t('org.storage.unlimited')
  return formatStorageBytes(value)
}
</script>

<template>
  <UCard>
    <template #header>
      <h2 class="font-semibold">
        {{ t('org.storage.nodesTitle') }}
      </h2>
    </template>
    <div
      v-if="!nodes.length"
      class="text-sm text-[var(--ogb-text-muted)]"
    >
      {{ t('org.storage.nodesEmpty') }}
    </div>
    <div
      v-for="node in nodes"
      :key="node.id"
      class="border-b py-4 last:border-b-0"
      style="border-color: var(--ogb-border);"
    >
      <div class="flex flex-wrap items-center gap-2">
        <span class="font-medium">{{ node.nodeId }}</span>
        <UBadge
          :color="node.isHealthy ? 'success' : 'warning'"
          variant="subtle"
        >
          {{ node.isHealthy ? t('org.storage.healthy') : t('org.storage.unhealthy') }}
        </UBadge>
        <UBadge
          color="neutral"
          variant="subtle"
        >
          {{ hostingScopeLabel(node.hostingScope ?? 0) }}
        </UBadge>
      </div>
      <div class="mt-1 text-xs text-[var(--ogb-text-muted)]">
        {{ t('org.storage.usedMaxLabel', {
          used: formatBytes(node.usedBytes ?? 0),
          max: formatBytes(node.maxBytes ?? 0),
        }) }}
      </div>
      <form
        v-if="editingNodeId === node.id"
        class="mt-3 grid gap-3 md:grid-cols-2"
        @submit.prevent="emit('saveEdit', node.id)"
      >
        <UFormField :label="t('org.storage.maxCapacityGiBLabel')">
          <UInput
            :model-value="editMaxGiB"
            type="number"
            min="1"
            @update:model-value="emit('update:editMaxGiB', Number($event))"
          />
        </UFormField>
        <UFormField :label="t('org.storage.hostingScopeLabel')">
          <USelect
            :model-value="editHostingScope"
            :items="hostingScopeOptions"
            @update:model-value="emit('update:editHostingScope', Number($event))"
          />
        </UFormField>
        <div class="flex gap-2 md:col-span-2">
          <UButton
            type="submit"
            size="sm"
            :loading="editLoading"
          >
            {{ t('org.storage.save') }}
          </UButton>
          <UButton
            size="sm"
            variant="ghost"
            @click="emit('cancelEdit')"
          >
            {{ t('org.storage.cancel') }}
          </UButton>
        </div>
      </form>
      <UButton
        v-else
        class="mt-2"
        size="xs"
        variant="soft"
        @click="emit('startEdit', node)"
      >
        {{ t('org.storage.editNode') }}
      </UButton>
    </div>
  </UCard>
</template>
