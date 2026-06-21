<script setup lang="ts">
import type { RepositoryContentRef } from '~/utils/api'

const props = defineProps<{
  branches: RepositoryContentRef[]
  tags: RepositoryContentRef[]
  modelValue: string
}>()

const emit = defineEmits<{
  'update:modelValue': [refName: string]
}>()

const { t } = useI18n()

const activeTab = ref<'branches' | 'tags'>('branches')

const currentRefs = computed(() =>
  activeTab.value === 'branches' ? props.branches : props.tags,
)

function selectRef(name: string) {
  emit('update:modelValue', name)
}

watch(
  () => [props.modelValue, props.branches, props.tags] as const,
  () => {
    if (props.branches.some(branch => branch.name === props.modelValue)) {
      activeTab.value = 'branches'
      return
    }
    if (props.tags.some(tag => tag.name === props.modelValue)) {
      activeTab.value = 'tags'
    }
  },
  { immediate: true },
)
</script>

<template>
  <div class="flex flex-wrap items-center gap-3">
    <div
      class="inline-flex rounded-md border border-[var(--ogb-border)] bg-[var(--ogb-surface)] p-0.5 text-sm"
      role="tablist"
    >
      <button
        type="button"
        role="tab"
        class="rounded px-3 py-1.5 transition-colors"
        :class="activeTab === 'branches'
          ? 'bg-[var(--ogb-bg)] font-medium text-[var(--ogb-text)]'
          : 'text-[var(--ogb-text-muted)] hover:text-[var(--ogb-text)]'"
        :aria-selected="activeTab === 'branches'"
        @click="activeTab = 'branches'"
      >
        {{ t('repo.browse.branchesTab') }}
      </button>
      <button
        type="button"
        role="tab"
        class="rounded px-3 py-1.5 transition-colors"
        :class="activeTab === 'tags'
          ? 'bg-[var(--ogb-bg)] font-medium text-[var(--ogb-text)]'
          : 'text-[var(--ogb-text-muted)] hover:text-[var(--ogb-text)]'"
        :aria-selected="activeTab === 'tags'"
        @click="activeTab = 'tags'"
      >
        {{ t('repo.browse.tagsTab') }}
      </button>
    </div>

    <label class="flex items-center gap-2 text-sm">
      <span class="text-[var(--ogb-text-muted)]">{{ t('repo.browse.refLabel') }}</span>
      <select
        :value="modelValue"
        class="rounded-md border border-[var(--ogb-border)] bg-[var(--ogb-surface)] px-2 py-1.5 font-mono text-sm"
        @change="selectRef(($event.target as HTMLSelectElement).value)"
      >
        <option
          v-if="currentRefs.length === 0"
          disabled
          value=""
        >
          {{ t('repo.browse.noRefs') }}
        </option>
        <option
          v-for="ref in currentRefs"
          :key="ref.name"
          :value="ref.name"
        >
          {{ ref.name }}
        </option>
      </select>
    </label>
  </div>
</template>
