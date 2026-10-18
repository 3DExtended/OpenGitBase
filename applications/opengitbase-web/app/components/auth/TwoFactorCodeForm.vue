<script setup lang="ts">
import { normalizeAuthenticatorCode } from '~/utils/twoFactor'

const props = defineProps<{
  loading?: boolean
  error?: string | null
  /** Pre-fills the field; only the visual gallery uses it. */
  initialCode?: string
}>()

const emit = defineEmits<{
  submit: [code: string]
  back: []
}>()

const { t } = useI18n()
const code = ref(props.initialCode ?? '')

function onSubmit() {
  emit('submit', normalizeAuthenticatorCode(code.value))
}
</script>

<template>
  <form
    class="space-y-4"
    data-testid="two-factor-code-form"
    @submit.prevent="onSubmit"
  >
    <p class="text-sm text-[var(--ogb-text-muted)]">
      {{ t('auth.twoFactor.instructions') }}
    </p>

    <UFormField
      :label="t('auth.twoFactor.codeLabel')"
      required
    >
      <UInput
        v-model="code"
        autocomplete="one-time-code"
        inputmode="numeric"
        maxlength="9"
        placeholder="123 456"
        class="w-full font-mono"
        autofocus
        required
      />
    </UFormField>

    <UAlert
      v-if="error"
      color="error"
      variant="subtle"
      :description="error"
    />

    <UButton
      type="submit"
      block
      :loading="loading"
    >
      {{ t('auth.twoFactor.submit') }}
    </UButton>

    <UButton
      variant="link"
      size="sm"
      class="px-0"
      icon="i-lucide-arrow-left"
      @click="emit('back')"
    >
      {{ t('auth.twoFactor.back') }}
    </UButton>
  </form>
</template>
