<script setup lang="ts">
import type { DomainAllowanceRequestDto } from '~/utils/api'

definePageMeta({ middleware: 'admin' })

const { t } = useI18n()
const api = useApi()

useHead({ title: t('admin.egress.title') })

const loading = ref(true)
const error = ref<string | null>(null)
const requests = ref<DomainAllowanceRequestDto[]>([])

const submitDomain = ref('registry.npmjs.org')
const submitJustification = ref('')
const submitLoading = ref(false)

async function refreshRequests() {
  loading.value = true
  error.value = null
  try {
    const result = await api.admin.egress.listPlatformRequests()
    requests.value = result.data ?? []
  }
  catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to load domain allowance requests.'
  }
  finally {
    loading.value = false
  }
}

async function submitRequest() {
  submitLoading.value = true
  try {
    await api.pipelines.submitDomainRequest({
      domain: submitDomain.value,
      justification: submitJustification.value,
      scope: 0,
    })
    submitJustification.value = ''
    await refreshRequests()
  }
  catch (e) {
    error.value = e instanceof Error ? e.message : 'Failed to submit domain request.'
  }
  finally {
    submitLoading.value = false
  }
}

async function review(requestId: string, approve: boolean) {
  if (approve) {
    await api.admin.egress.approvePlatformRequest(requestId)
  }
  else {
    await api.admin.egress.denyPlatformRequest(requestId)
  }
  await refreshRequests()
}

onMounted(() => {
  void refreshRequests()
})
</script>

<template>
  <div class="mx-auto max-w-4xl space-y-6">
    <div>
      <h1 class="text-2xl font-semibold">
        {{ t('admin.egress.title') }}
      </h1>
      <p class="text-sm text-[var(--ogb-text-muted)]">
        {{ t('admin.egress.description') }}
      </p>
    </div>

    <UAlert
      v-if="error"
      color="error"
      variant="soft"
      :title="error"
    />

    <UCard>
      <template #header>
        <h2 class="font-semibold">
          {{ t('admin.egress.submitTitle') }}
        </h2>
      </template>
      <form
        class="grid gap-3"
        @submit.prevent="submitRequest"
      >
        <UInput
          v-model="submitDomain"
          :label="t('admin.egress.domainLabel')"
        />
        <UTextarea
          v-model="submitJustification"
          :label="t('admin.egress.justificationLabel')"
        />
        <UButton
          type="submit"
          :loading="submitLoading"
        >
          {{ t('admin.egress.submitRequest') }}
        </UButton>
      </form>
    </UCard>

    <UCard>
      <template #header>
        <h2 class="font-semibold">
          {{ t('admin.egress.pendingTitle') }}
        </h2>
      </template>
      <div
        v-if="loading"
        class="text-sm text-[var(--ogb-text-muted)]"
      >
        {{ t('common.loading') }}
      </div>
      <div
        v-else-if="!requests.length"
        class="text-sm text-[var(--ogb-text-muted)]"
      >
        {{ t('admin.egress.pendingEmpty') }}
      </div>
      <div
        v-else
        class="space-y-3"
      >
        <div
          v-for="request in requests"
          :key="request.id"
          class="rounded border p-3"
          style="border-color: var(--ogb-border);"
        >
          <p class="font-medium">
            {{ request.domain }}
          </p>
          <p class="text-sm text-[var(--ogb-text-muted)]">
            {{ request.justification }}
          </p>
          <div class="mt-2 flex gap-2">
            <UButton
              size="xs"
              @click="review(request.id, true)"
            >
              {{ t('admin.egress.approve') }}
            </UButton>
            <UButton
              size="xs"
              color="error"
              variant="soft"
              @click="review(request.id, false)"
            >
              {{ t('admin.egress.deny') }}
            </UButton>
          </div>
        </div>
      </div>
    </UCard>
  </div>
</template>
