<script setup lang="ts">
import type { Repository, RepositoryMember } from '~/utils/api'

definePageMeta({ middleware: 'auth' })

const route = useRoute()
const { t } = useI18n()
const api = useApi()

const owner = computed(() => String(route.params.owner))
const repoSlug = computed(() => String(route.params.repo))

const repo = ref<Repository | null>(null)
const members = ref<RepositoryMember[]>([])
const loading = ref(true)

const username = ref('')
const role = ref(1)
const adding = ref(false)
const addError = ref<string | null>(null)

const roleOptions = [
  { label: t('repo.members.roles.reader'), value: 1 },
  { label: t('repo.members.roles.writer'), value: 2 },
  { label: t('repo.members.roles.admin'), value: 3 },
]

useHead({ title: t('repo.members.title') })

function roleLabel(value: number) {
  return roleOptions.find(r => r.value === value)?.label ?? String(value)
}

onMounted(async () => {
  loading.value = true
  const result = await api.repositories.getBySlug(owner.value, repoSlug.value)
  if (result.data) {
    repo.value = result.data
  }
  else {
    const list = await api.repositories.list()
    repo.value = list.data?.find(r => r.slug === repoSlug.value) ?? null
  }

  if (repo.value) {
    const membersResult = await api.repositoryMembers.list(repo.value.id)
    members.value = membersResult.data ?? []
  }
  loading.value = false
})

async function addMember() {
  if (!repo.value) {
    return
  }
  adding.value = true
  addError.value = null
  try {
    const result = await api.repositoryMembers.create({
      modelToCreate: {
        repositoryId: repo.value.id,
        userId: username.value,
        role: role.value,
      },
    })
    if (result.error) {
      addError.value = result.error
      return
    }
    username.value = ''
    const membersResult = await api.repositoryMembers.list(repo.value.id)
    members.value = membersResult.data ?? []
  }
  finally {
    adding.value = false
  }
}

async function removeMember(id: string) {
  if (!repo.value) {
    return
  }
  await api.repositoryMembers.delete(id)
  const membersResult = await api.repositoryMembers.list(repo.value.id)
  members.value = membersResult.data ?? []
}
</script>

<template>
  <div class="mx-auto max-w-2xl space-y-6">
    <UButton
      :to="`/${owner}/${repoSlug}`"
      variant="ghost"
      icon="i-lucide-arrow-left"
      size="sm"
    >
      {{ owner }}/{{ repoSlug }}
    </UButton>

    <h1 class="text-2xl font-semibold">
      {{ t('repo.members.title') }}
    </h1>

    <div
      v-if="loading"
      class="text-sm text-[var(--ogb-text-muted)]"
    >
      {{ t('common.loading') }}
    </div>

    <template v-else-if="repo">
      <UCard>
        <template #header>
          <h2 class="font-semibold">
            {{ t('repo.members.addTitle') }}
          </h2>
        </template>
        <form
          class="space-y-4"
          @submit.prevent="addMember"
        >
          <UFormField
            :label="t('auth.fields.username')"
            required
          >
            <UInput v-model="username" />
          </UFormField>
          <UFormField :label="t('repo.members.roleLabel')">
            <USelect
              v-model="role"
              :items="roleOptions"
            />
          </UFormField>
          <UAlert
            v-if="addError"
            color="error"
            variant="subtle"
            :description="addError"
          />
          <UButton
            type="submit"
            :loading="adding"
          >
            {{ t('repo.members.addButton') }}
          </UButton>
        </form>
      </UCard>

      <UCard>
        <template #header>
          <h2 class="font-semibold">
            {{ t('repo.members.listTitle') }}
          </h2>
        </template>

        <p
          v-if="!members.length"
          class="text-sm text-[var(--ogb-text-muted)]"
        >
          {{ t('repo.members.empty') }}
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
            <UButton
              color="error"
              variant="ghost"
              size="sm"
              icon="i-lucide-user-minus"
              @click="removeMember(member.id)"
            />
          </li>
        </ul>
      </UCard>
    </template>
  </div>
</template>
