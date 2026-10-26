<script setup lang="ts">
import { recoveryCodesFileContent } from '~/utils/twoFactor'

const props = defineProps<{
  codes: string[]
  /** The username, written into the downloaded file. */
  account: string
  /** Pre-ticks the confirmation; only the visual gallery uses it. */
  initialStored?: boolean
}>()

const emit = defineEmits<{
  done: []
}>()

const { t } = useI18n()
const toast = useToast()
const requestUrl = useRequestURL()
const stored = ref(props.initialStored ?? false)

async function copyCodes() {
  await navigator.clipboard.writeText(props.codes.join('\n'))
  toast.add({ title: t('settings.security.recoveryCodes.copied'), color: 'success' })
}

function downloadCodes() {
  const content = recoveryCodesFileContent(props.codes, {
    account: props.account,
    site: requestUrl.host,
    createdAt: new Date(),
  })
  const url = URL.createObjectURL(new Blob([content], { type: 'text/plain' }))
  const anchor = document.createElement('a')
  anchor.href = url
  anchor.download = `opengitbase-recovery-codes-${props.account}.txt`
  anchor.click()
  URL.revokeObjectURL(url)
}
</script>

<template>
  <div
    class="space-y-4"
    data-testid="recovery-codes-panel"
  >
    <p class="text-sm text-[var(--ogb-text-muted)]">
      {{ t('settings.security.recoveryCodes.description') }}
    </p>

    <ul
      class="grid grid-cols-2 gap-x-6 gap-y-2 rounded-md border border-[var(--ogb-border)] bg-[var(--ogb-raised)] p-4 font-mono text-sm"
      data-testid="recovery-codes-list"
    >
      <li
        v-for="code in codes"
        :key="code"
      >
        {{ code }}
      </li>
    </ul>

    <div class="flex flex-wrap gap-2">
      <UButton
        variant="soft"
        icon="i-lucide-copy"
        @click="copyCodes"
      >
        {{ t('settings.security.recoveryCodes.copy') }}
      </UButton>
      <UButton
        variant="soft"
        icon="i-lucide-download"
        @click="downloadCodes"
      >
        {{ t('settings.security.recoveryCodes.download') }}
      </UButton>
    </div>

    <UCheckbox
      v-model="stored"
      :label="t('settings.security.recoveryCodes.stored')"
      data-testid="recovery-codes-stored"
    />

    <div class="flex justify-end">
      <UButton
        :disabled="!stored"
        data-testid="recovery-codes-done"
        @click="emit('done')"
      >
        {{ t('settings.security.recoveryCodes.done') }}
      </UButton>
    </div>
  </div>
</template>
