<script setup lang="ts">
import type { OrganizationInvitePublic } from '~/utils/api'

const route = useRoute()
const { t } = useI18n()
const api = useApi()
const auth = useAuth()

const token = computed(() => String(route.params.token))

const invite = ref<OrganizationInvitePublic | null>(null)
const loading = ref(true)
const notFound = ref(false)
const acting = ref(false)
const message = ref<string | null>(null)
const done = ref(false)

const roleOptions = [
  { label: t('org.members.roles.member'), value: 0 },
  { label: t('org.members.roles.owner'), value: 1 },
]

useHead({ title: t('invite.title') })

function roleLabel(value: number) {
  return roleOptions.find(r => r.value === value)?.label ?? String(value)
}

const isExpired = computed(() => invite.value?.status === 4)
const isPending = computed(() => invite.value?.status === 0)

onMounted(async () => {
  loading.value = true
  const result = await api.invite.getByToken(token.value)
  if (result.status === 404 || !result.data) {
    notFound.value = true
    loading.value = false
    return
  }
  invite.value = result.data
  loading.value = false
})

async function acceptInvite() {
  if (!auth.isAuthenticated) {
    await navigateTo(`/sign-in?redirect=${encodeURIComponent(route.fullPath)}`)
    return
  }
  acting.value = true
  message.value = null
  try {
    const result = await api.invite.accept(token.value)
    if (result.error) {
      message.value = result.error
      return
    }
    done.value = true
    message.value = t('invite.accepted')
    if (invite.value?.organizationSlug) {
      await navigateTo(`/${invite.value.organizationSlug}/members`)
    }
  }
  finally {
    acting.value = false
  }
}

async function declineInvite() {
  acting.value = true
  try {
    await api.invite.decline(token.value)
    done.value = true
    message.value = t('invite.declined')
  }
  finally {
    acting.value = false
  }
}
</script>

<template>
  <div class="mx-auto max-w-md space-y-6 py-8">
    <h1 class="text-2xl font-semibold">
      {{ t('invite.title') }}
    </h1>

    <div
      v-if="loading"
      class="text-sm text-[var(--ogb-text-muted)]"
    >
      {{ t('common.loading') }}
    </div>

    <UCard v-else-if="notFound">
      <p class="text-sm text-[var(--ogb-text-muted)]">
        {{ t('invite.notFound') }}
      </p>
    </UCard>

    <UCard v-else-if="invite">
      <div class="space-y-4">
        <div>
          <p class="text-xs text-[var(--ogb-text-muted)]">
            {{ t('invite.organization') }}
          </p>
          <p class="font-medium">
            {{ invite.organizationName }}
          </p>
        </div>
        <div>
          <p class="text-xs text-[var(--ogb-text-muted)]">
            {{ t('invite.role') }}
          </p>
          <p class="font-medium">
            {{ roleLabel(invite.role) }}
          </p>
        </div>

        <UAlert
          v-if="isExpired"
          color="warning"
          variant="subtle"
          :description="t('invite.expired')"
        />

        <UAlert
          v-if="message"
          :color="done ? 'success' : 'error'"
          variant="subtle"
          :description="message"
        />

        <template v-if="isPending && !done">
          <p
            v-if="!auth.isAuthenticated"
            class="text-sm text-[var(--ogb-text-muted)]"
          >
            {{ t('invite.signInRequired') }}
          </p>
          <div class="flex flex-wrap gap-2">
            <UButton
              v-if="!auth.isAuthenticated"
              to="/sign-in"
              variant="soft"
            >
              {{ t('invite.signIn') }}
            </UButton>
            <UButton
              v-if="!auth.isAuthenticated"
              to="/sign-up"
              variant="ghost"
            >
              {{ t('invite.signUp') }}
            </UButton>
            <UButton
              v-if="auth.isAuthenticated"
              :loading="acting"
              @click="acceptInvite"
            >
              {{ t('invite.accept') }}
            </UButton>
            <UButton
              variant="ghost"
              color="error"
              :loading="acting"
              @click="declineInvite"
            >
              {{ t('invite.decline') }}
            </UButton>
          </div>
        </template>
      </div>
    </UCard>
  </div>
</template>
