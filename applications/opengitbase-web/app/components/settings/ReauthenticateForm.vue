<script setup lang="ts">
import { normalizeAuthenticatorCode } from '~/utils/twoFactor'

const props = defineProps<{
  needsPassword: boolean
  needsCode: boolean
  loading?: boolean
  error?: string | null
}>()

const emit = defineEmits<{
  submit: [credentials: { password: string, code?: string }]
  cancel: []
}>()

const { t } = useI18n()
const password = ref('')
const code = ref('')

function onSubmit() {
  emit('submit', {
    password: props.needsPassword ? password.value : '',
    code: props.needsCode ? normalizeAuthenticatorCode(code.value) : undefined,
  })
}
</script>

<template>
  <form
    class="space-y-4"
    data-testid="reauthenticate-form"
    @submit.prevent="onSubmit"
  >
    <p class="text-sm text-[var(--ogb-text-muted)]">
      {{ t('settings.security.reauth.description') }}
    </p>

    <UFormField
      v-if="needsPassword"
      :label="t('auth.fields.password')"
      required
    >
      <UInput
        v-model="password"
        type="password"
        autocomplete="current-password"
        class="w-full"
        required
      />
    </UFormField>

    <UFormField
      v-if="needsCode"
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
        required
      />
    </UFormField>

    <UAlert
      v-if="error"
      color="error"
      variant="subtle"
      :description="error"
    />

    <div class="flex justify-end gap-2">
      <UButton
        variant="ghost"
        @click="emit('cancel')"
      >
        {{ t('common.cancel') }}
      </UButton>
      <UButton
        type="submit"
        :loading="loading"
      >
        {{ t('settings.security.reauth.submit') }}
      </UButton>
    </div>
  </form>
</template>
