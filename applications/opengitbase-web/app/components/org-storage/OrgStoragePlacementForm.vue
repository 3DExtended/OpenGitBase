<script setup lang="ts">
defineProps<{
  placementPolicy: number
  selfHostPreference: number
  saving?: boolean
  placementOptions: Array<{ label: string, value: number }>
  selfHostOptions: Array<{ label: string, value: number }>
}>()

const emit = defineEmits<{
  'update:placementPolicy': [value: number]
  'update:selfHostPreference': [value: number]
  save: []
}>()

const { t } = useI18n()
</script>

<template>
  <UCard>
    <template #header>
      <h2 class="font-semibold">
        {{ t('org.storage.placementTitle') }}
      </h2>
    </template>
    <form
      class="grid gap-4"
      @submit.prevent="emit('save')"
    >
      <UFormField :label="t('org.storage.placementPolicyLabel')">
        <USelect
          :model-value="placementPolicy"
          :items="placementOptions"
          @update:model-value="emit('update:placementPolicy', Number($event))"
        />
      </UFormField>
      <UFormField :label="t('org.storage.selfHostPreferenceLabel')">
        <USelect
          :model-value="selfHostPreference"
          :items="selfHostOptions"
          @update:model-value="emit('update:selfHostPreference', Number($event))"
        />
      </UFormField>
      <UButton
        type="submit"
        :loading="saving"
      >
        {{ t('org.storage.saveSettings') }}
      </UButton>
    </form>
  </UCard>
</template>
