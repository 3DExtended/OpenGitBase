<script setup lang="ts">
import type { Organization, OrganizationInvite, OrganizationMember } from '~/utils/api'

definePageMeta({ middleware: 'auth' })

const route = useRoute()
const { t } = useI18n()
const api = useApi()
const auth = useAuth()

const orgSlug = computed(() => String(route.params.owner))

const organization = ref<Organization | null>(null)
const members = ref<OrganizationMember[]>([])
const invites = ref<OrganizationInvite[]>([])
const loading = ref(true)
const forbidden = ref(false)
const notFound = ref(false)

const identifier = ref('')
const addRole = ref(1)
const adding = ref(false)
const addError = ref<string | null>(null)
const addSuccess = ref<string | null>(null)

const roleOptions = [
  { label: t('org.members.roles.member'), value: 0 },
  { label: t('org.members.roles.owner'), value: 1 },
]

useHead({ title: t('org.members.title') })

const currentMember = computed(() =>
  members.value.find(m => m.username?.toLowerCase() === auth.user?.username.toLowerCase()),
)

const isOwner = computed(() => currentMember.value?.role === 1)

const ownerCount = computed(() => members.value.filter(m => m.role === 1).length)

const isLastOwner = computed(() =>
  isOwner.value && ownerCount.value === 1,
)

function roleLabel(value: number) {
  return roleOptions.find(r => r.value === value)?.label ?? String(value)
}

async function loadInvites() {
  if (!organization.value) {
    return
  }
  const invitesResult = await api.organizations.invites.list(organization.value.id)
  invites.value = invitesResult.data ?? []
}

async function loadMembers() {
  if (!organization.value) {
    return
  }
  const membersResult = await api.organizations.members.list(organization.value.id)
  members.value = membersResult.data ?? []
  await loadInvites()
}

onMounted(async () => {
  loading.value = true
  forbidden.value = false
  notFound.value = false

  const orgResult = await api.organizations.getBySlug(orgSlug.value)
  if (orgResult.status === 404 || !orgResult.data) {
    notFound.value = true
    loading.value = false
    return
  }

  organization.value = orgResult.data
  const membersResult = await api.organizations.members.list(orgResult.data.id)

  if (membersResult.status === 403) {
    forbidden.value = true
    loading.value = false
    return
  }

  if (membersResult.error) {
    notFound.value = true
    loading.value = false
    return
  }

  members.value = membersResult.data ?? []
  await loadInvites()
  loading.value = false
})

async function updateRole(member: OrganizationMember, role: number) {
  if (!organization.value || member.role === role) {
    return
  }
  await api.organizations.members.updateRole(organization.value.id, member.userId, { role })
  await loadMembers()
}

async function removeMember(member: OrganizationMember) {
  if (!organization.value) {
    return
  }
  await api.organizations.members.remove(organization.value.id, member.userId)
  await loadMembers()
}

async function leaveOrganization() {
  if (!organization.value || !currentMember.value) {
    return
  }
  await api.organizations.members.remove(organization.value.id, currentMember.value.userId)
  await navigateTo(`/${orgSlug.value}`)
}

async function addMember() {
  if (!organization.value) {
    return
  }
  adding.value = true
  addError.value = null
  addSuccess.value = null
  try {
    const result = await api.organizations.members.add(organization.value.id, {
      identifier: identifier.value,
      role: addRole.value,
    })
    if (result.error) {
      addError.value = result.error
      return
    }
    identifier.value = ''
    if (result.status === 202) {
      addSuccess.value = t('org.members.invites.sent')
    }
    await loadMembers()
  }
  finally {
    adding.value = false
  }
}

async function resendInvite(invite: OrganizationInvite) {
  if (!organization.value) {
    return
  }
  await api.organizations.invites.resend(organization.value.id, invite.id)
  await loadInvites()
}

async function revokeInvite(invite: OrganizationInvite) {
  if (!organization.value) {
    return
  }
  await api.organizations.invites.revoke(organization.value.id, invite.id)
  await loadInvites()
}
</script>

<template>
  <div class="mx-auto max-w-2xl space-y-6">
    <UButton
      :to="`/${orgSlug}`"
      variant="ghost"
      icon="i-lucide-arrow-left"
      size="sm"
    >
      {{ orgSlug }}
    </UButton>

    <div class="flex flex-wrap items-center justify-between gap-4">
      <h1 class="text-2xl font-semibold">
        {{ t('org.members.title') }}
      </h1>
      <UButton
        v-if="currentMember && !isLastOwner"
        color="error"
        variant="soft"
        size="sm"
        @click="leaveOrganization"
      >
        {{ t('org.members.leave') }}
      </UButton>
    </div>

    <div
      v-if="loading"
      class="text-sm text-[var(--ogb-text-muted)]"
    >
      {{ t('common.loading') }}
    </div>

    <UCard v-else-if="notFound">
      <p class="text-sm text-[var(--ogb-text-muted)]">
        {{ t('org.members.notFound') }}
      </p>
    </UCard>

    <UCard v-else-if="forbidden">
      <p class="text-sm text-[var(--ogb-text-muted)]">
        {{ t('org.members.forbidden') }}
      </p>
    </UCard>

    <template v-else-if="organization">
      <UCard
        v-if="isOwner"
        class="mb-6"
      >
        <template #header>
          <h2 class="font-semibold">
            {{ t('org.members.addTitle') }}
          </h2>
        </template>
        <form
          class="space-y-4"
          @submit.prevent="addMember"
        >
          <UFormField
            :label="t('org.members.identifierLabel')"
            required
          >
            <UInput
              v-model="identifier"
              :placeholder="t('org.members.identifierPlaceholder')"
            />
          </UFormField>
          <UFormField :label="t('org.members.roleLabel')">
            <USelect
              v-model="addRole"
              :items="roleOptions"
            />
          </UFormField>
          <UAlert
            v-if="addError"
            color="error"
            variant="subtle"
            :description="addError"
          />
          <UAlert
            v-if="addSuccess"
            color="success"
            variant="subtle"
            :description="addSuccess"
          />
          <UButton
            type="submit"
            :loading="adding"
          >
            {{ t('org.members.addButton') }}
          </UButton>
        </form>
      </UCard>

      <UCard>
        <template #header>
          <h2 class="font-semibold">
            {{ t('org.members.listTitle') }}
          </h2>
        </template>

        <p
          v-if="!members.length"
          class="text-sm text-[var(--ogb-text-muted)]"
        >
          {{ t('org.members.empty') }}
        </p>

        <ul
          v-else
          class="divide-y"
          style="border-color: var(--ogb-border);"
        >
          <li
            v-for="member in members"
            :key="member.id"
            class="flex items-center justify-between gap-4 py-3 first:pt-0 last:pb-0"
          >
            <div class="min-w-0 flex-1">
              <p class="font-medium">
                {{ member.username ?? member.userId }}
              </p>
              <USelect
                v-if="isOwner"
                :model-value="member.role"
                :items="roleOptions"
                size="sm"
                class="mt-1 max-w-xs"
                @update:model-value="updateRole(member, $event as number)"
              />
              <p
                v-else
                class="text-xs text-[var(--ogb-text-muted)]"
              >
                {{ roleLabel(member.role) }}
              </p>
            </div>
            <UButton
              v-if="isOwner && member.userId !== currentMember?.userId"
              color="error"
              variant="ghost"
              size="sm"
              icon="i-lucide-user-minus"
              @click="removeMember(member)"
            />
          </li>
        </ul>
      </UCard>

      <UCard
        v-if="invites.length"
        class="mt-6"
      >
        <template #header>
          <h2 class="font-semibold">
            {{ t('org.members.invites.title') }}
          </h2>
        </template>
        <ul
          class="divide-y"
          style="border-color: var(--ogb-border);"
        >
          <li
            v-for="invite in invites"
            :key="invite.id"
            class="flex items-center justify-between gap-4 py-3 first:pt-0 last:pb-0"
          >
            <div>
              <p class="font-medium">
                {{ invite.email }}
              </p>
              <p class="text-xs text-[var(--ogb-text-muted)]">
                {{ roleLabel(invite.role) }} · {{ t('org.members.invites.expires', { date: new Date(invite.expiresAt).toLocaleDateString() }) }}
              </p>
            </div>
            <div
              v-if="isOwner"
              class="flex gap-2"
            >
              <UButton
                variant="ghost"
                size="sm"
                @click="resendInvite(invite)"
              >
                {{ t('org.members.invites.resend') }}
              </UButton>
              <UButton
                color="error"
                variant="ghost"
                size="sm"
                @click="revokeInvite(invite)"
              >
                {{ t('org.members.invites.revoke') }}
              </UButton>
            </div>
          </li>
        </ul>
      </UCard>
    </template>
  </div>
</template>
