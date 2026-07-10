<script setup lang="ts">
import type { PublicStatusIncident } from '~/utils/publicStatus'

definePageMeta({ middleware: 'admin' })

const { t } = useI18n()
const api = useApi()

useHead({ title: t('admin.status.title') })

const loading = ref(true)
const saving = ref(false)
const error = ref<string | null>(null)
const message = ref('')
const severity = ref<'Info' | 'Warning' | 'Outage'>('Warning')
const activeIncident = ref<PublicStatusIncident | null>(null)

async function refresh() {
  loading.value = true
  error.value = null
  const result = await api.admin.status.getIncident()
  activeIncident.value = result.data
  if (result.data) {
    message.value = result.data.message
    severity.value = result.data.severity === 2
      ? 'Outage'
      : result.data.severity === 1
        ? 'Warning'
        : 'Info'
  }
  loading.value = false
}

async function saveIncident() {
  saving.value = true
  error.value = null
  const result = await api.admin.status.setIncident({
    message: message.value.trim(),
    severity: severity.value,
  })
  if (result.error) {
    error.value = result.error
  }
  else {
    activeIncident.value = result.data
  }
  saving.value = false
}

async function resolveIncident() {
  saving.value = true
  error.value = null
  const result = await api.admin.status.resolveIncident()
  if (result.error) {
    error.value = result.error
  }
  else {
    activeIncident.value = null
    message.value = ''
    severity.value = 'Warning'
  }
  saving.value = false
}

onMounted(refresh)
</script>

<template>
  <div class="mx-auto max-w-3xl space-y-6">
    <div class="flex flex-wrap items-center justify-between gap-3">
      <div>
        <UButton
          to="/admin"
          variant="ghost"
          color="neutral"
          icon="i-lucide-arrow-left"
          size="sm"
        >
          {{ t('admin.title') }}
        </UButton>
        <h1 class="mt-2 text-2xl font-semibold">
          {{ t('admin.status.title') }}
        </h1>
        <p class="mt-1 text-sm text-[var(--ogb-text-muted)]">
          {{ t('admin.status.description') }}
        </p>
      </div>
      <UButton
        to="/status"
        variant="outline"
        color="neutral"
        trailing-icon="i-lucide-external-link"
      >
        {{ t('admin.status.preview') }}
      </UButton>
    </div>

    <UAlert
      v-if="error"
      color="error"
      variant="subtle"
      :title="t('admin.status.error')"
      :description="error"
    />

    <UCard>
      <div class="space-y-4">
        <UAlert
          v-if="activeIncident"
          color="warning"
          variant="subtle"
          :title="t('admin.status.activeTitle')"
          :description="activeIncident.message"
        />

        <UFormField :label="t('admin.status.messageLabel')">
          <UTextarea
            v-model="message"
            :rows="4"
            maxlength="500"
            :placeholder="t('admin.status.messagePlaceholder')"
          />
        </UFormField>

        <UFormField :label="t('admin.status.severityLabel')">
          <USelect
            v-model="severity"
            :items="[
              { label: t('admin.status.severity.info'), value: 'Info' },
              { label: t('admin.status.severity.warning'), value: 'Warning' },
              { label: t('admin.status.severity.outage'), value: 'Outage' },
            ]"
          />
        </UFormField>

        <div class="flex flex-wrap gap-2">
          <UButton
            :loading="saving"
            icon="i-lucide-save"
            @click="saveIncident"
          >
            {{ t('admin.status.save') }}
          </UButton>
          <UButton
            v-if="activeIncident"
            color="neutral"
            variant="outline"
            :loading="saving"
            icon="i-lucide-check"
            @click="resolveIncident"
          >
            {{ t('admin.status.resolve') }}
          </UButton>
          <UButton
            to="/admin/storage"
            color="neutral"
            variant="ghost"
            icon="i-lucide-server"
          >
            {{ t('admin.storage.title') }}
          </UButton>
        </div>
      </div>
    </UCard>
  </div>
</template>
