<script setup lang="ts">
import type { Organization, OrganizationMember } from '~/utils/api'

definePageMeta({ middleware: 'auth' })

const route = useRoute()
const { t } = useI18n()
const api = useApi()

const orgSlug = computed(() => String(route.params.owner))

const organization = ref<Organization | null>(null)
const members = ref<OrganizationMember[]>([])
const loading = ref(true)
const forbidden = ref(false)
const notFound = ref(false)

const roleOptions = [
  { label: t('org.members.roles.member'), value: 0 },
  { label: t('org.members.roles.owner'), value: 1 },
]

useHead({ title: t('org.members.title') })

function roleLabel(value: number) {
  return roleOptions.find(r => r.value === value)?.label ?? String(value)
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
  loading.value = false
})
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

    <h1 class="text-2xl font-semibold">
      {{ t('org.members.title') }}
    </h1>

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

    <UCard v-else-if="organization">
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
          <div>
            <p class="font-medium">
              {{ member.username ?? member.userId }}
            </p>
            <p class="text-xs text-[var(--ogb-text-muted)]">
              {{ roleLabel(member.role) }}
            </p>
          </div>
        </li>
      </ul>
    </UCard>
  </div>
</template>
