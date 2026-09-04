<script setup lang="ts">
import type { Repository, RepositoryByteOverrideEligibility, RepositoryUsage } from '~/utils/api'

definePageMeta({ middleware: 'auth' })

const route = useRoute()
const { t } = useI18n()
const api = useApi()

const owner = computed(() => String(route.params.owner))
const repoSlug = computed(() => String(route.params.repo))

const repo = ref<Repository | null>(null)
const usage = ref<RepositoryUsage | null>(null)
const byteOverrideEligibility = ref<RepositoryByteOverrideEligibility | null>(null)
const loading = ref(true)
const byteOverrideLoading = ref(false)
const byteOverrideSaving = ref(false)
const byteOverrideError = ref<string | null>(null)
const byteOverrideSuccess = ref(false)

const name = ref('')
const isPrivate = ref(false)
const saving = ref(false)
const saveError = ref<string | null>(null)
const saveSuccess = ref(false)
const deleting = ref(false)
const showDeleteConfirm = ref(false)

useHead({ title: t('repo.settings.title') })

const sections = computed(() => [
  { id: 'general', label: t('repo.settings.nav.general') },
  { id: 'branches', label: t('repo.settings.nav.branches') },
  { id: 'discussions', label: t('repo.settings.nav.discussions') },
  { id: 'storage', label: t('repo.settings.nav.storage') },
  { id: 'danger', label: t('repo.settings.nav.danger') },
])
const activeSection = ref('general')
let sectionObserver: IntersectionObserver | null = null

function navItemClass(id: string): string {
  return activeSection.value === id
    ? 'rounded-md border border-[var(--ogb-border)] bg-[var(--ogb-raised)] px-3 py-2 !text-[var(--ogb-accent)]'
    : 'rounded-md px-3 py-2 text-[var(--ogb-text-muted)] transition-colors hover:text-[var(--ogb-text)]'
}

function setupScrollSpy() {
  if (!import.meta.client) {
    return
  }
  sectionObserver?.disconnect()
  const els = sections.value
    .map(section => document.getElementById(section.id))
    .filter((el): el is HTMLElement => el !== null)
  if (!els.length) {
    return
  }
  sectionObserver = new IntersectionObserver(
    (entries) => {
      const visible = entries
        .filter(entry => entry.isIntersecting)
        .sort((a, b) => a.boundingClientRect.top - b.boundingClientRect.top)
      if (visible[0]) {
        activeSection.value = visible[0].target.id
      }
    },
    { rootMargin: '-72px 0px -70% 0px', threshold: 0 },
  )
  els.forEach(el => sectionObserver!.observe(el))
}

onBeforeUnmount(() => sectionObserver?.disconnect())

async function loadByteOverrideEligibility(repositoryId: string) {
  byteOverrideLoading.value = true
  byteOverrideError.value = null
  try {
    const result = await api.repositories.byteOverrideEligibility(repositoryId)
    byteOverrideEligibility.value = result.data
  }
  finally {
    byteOverrideLoading.value = false
  }
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
    name.value = repo.value.name
    isPrivate.value = repo.value.isPrivate
    const usageResult = await api.repositories.usage(repo.value.id)
    usage.value = usageResult.data
    await loadByteOverrideEligibility(repo.value.id)
  }
  loading.value = false
  await nextTick()
  setupScrollSpy()
})

async function saveByteOverride(maxBytesOverride: number | null) {
  if (!repo.value) {
    return
  }
  byteOverrideSaving.value = true
  byteOverrideError.value = null
  byteOverrideSuccess.value = false
  try {
    const result = await api.repositories.updateMaxBytesOverride(repo.value.id, { maxBytesOverride })
    if (result.error || !result.data) {
      byteOverrideError.value = result.error ?? t('repo.byteOverride.saveFailed')
      return
    }
    repo.value = result.data
    byteOverrideSuccess.value = true
    await loadByteOverrideEligibility(repo.value.id)
    const usageResult = await api.repositories.usage(repo.value.id)
    usage.value = usageResult.data
  }
  finally {
    byteOverrideSaving.value = false
  }
}

async function save() {
  if (!repo.value) {
    return
  }
  saving.value = true
  saveError.value = null
  saveSuccess.value = false
  try {
    const result = await api.repositories.update(repo.value.id, {
      name: name.value,
      isPrivate: isPrivate.value,
    })
    if (result.error) {
      saveError.value = result.error
      return
    }
    saveSuccess.value = true
  }
  finally {
    saving.value = false
  }
}

async function deleteRepo() {
  if (!repo.value) {
    return
  }
  deleting.value = true
  try {
    await api.repositories.delete(repo.value.id)
    await navigateTo(`/${owner.value}`)
  }
  finally {
    deleting.value = false
    showDeleteConfirm.value = false
  }
}
</script>

<template>
  <div class="mx-auto max-w-5xl">
    <UButton
      :to="`/${owner}/${repoSlug}`"
      variant="ghost"
      icon="i-lucide-arrow-left"
      size="sm"
    >
      {{ owner }}/{{ repoSlug }}
    </UButton>

    <h1 class="mt-4 text-2xl font-semibold">
      {{ t('repo.settings.title') }}
    </h1>
    <p class="mt-1 text-sm text-[var(--ogb-text-muted)]">
      {{ owner }}/{{ repoSlug }}
    </p>

    <div
      v-if="loading"
      class="mt-6 text-sm text-[var(--ogb-text-muted)]"
    >
      {{ t('common.loading') }}
    </div>

    <div
      v-else-if="repo"
      class="mt-6 grid gap-8 md:grid-cols-[12rem_minmax(0,1fr)]"
    >
      <nav class="hidden md:block">
        <div class="sticky top-20 flex flex-col gap-1 text-sm">
          <a
            v-for="section in sections"
            :key="section.id"
            :href="`#${section.id}`"
            :class="navItemClass(section.id)"
          >
            {{ section.label }}
          </a>
        </div>
      </nav>

      <div class="space-y-4">
        <section
          id="general"
          class="scroll-mt-24"
        >
          <UCard>
            <template #header>
              <h2 class="font-semibold">
                {{ t('repo.settings.nav.general') }}
              </h2>
            </template>
            <form
              class="space-y-4"
              @submit.prevent="save"
            >
              <UFormField
                :label="t('repo.create.nameLabel')"
                required
              >
                <UInput v-model="name" />
              </UFormField>

              <UCheckbox
                v-model="isPrivate"
                :label="t('repo.create.privateLabel')"
              />

              <UAlert
                v-if="saveSuccess"
                color="success"
                variant="subtle"
                :description="t('repo.settings.saved')"
              />
              <UAlert
                v-if="saveError"
                color="error"
                variant="subtle"
                :description="saveError"
              />

              <UButton
                type="submit"
                :loading="saving"
              >
                {{ t('common.save') }}
              </UButton>
            </form>
          </UCard>
        </section>

        <section
          id="branches"
          class="scroll-mt-24"
        >
          <UCard>
            <template #header>
              <h2 class="font-semibold">
                {{ t('repo.mergeRequests.branchesTitle') }}
              </h2>
            </template>
            <p class="text-sm text-[var(--ogb-text-muted)]">
              {{ t('repo.mergeRequests.branchesDescription') }}
            </p>
            <UButton
              :to="`/${owner}/${repoSlug}/settings/branches`"
              variant="soft"
              class="mt-4"
              icon="i-lucide-git-branch-plus"
            >
              {{ t('repo.mergeRequests.manageBranches') }}
            </UButton>
          </UCard>
        </section>

        <section
          id="discussions"
          class="scroll-mt-24"
        >
          <UCard>
            <template #header>
              <h2 class="font-semibold">
                {{ t('repo.discussions.settings.title') }}
              </h2>
            </template>
            <p class="text-sm text-[var(--ogb-text-muted)]">
              {{ t('repo.discussions.settings.linkDescription') }}
            </p>
            <UButton
              :to="`/${owner}/${repoSlug}/settings/discussions`"
              variant="soft"
              class="mt-4"
              icon="i-lucide-shield-ban"
            >
              {{ t('repo.discussions.settings.manageLink') }}
            </UButton>
          </UCard>
        </section>

        <section
          id="storage"
          class="scroll-mt-24 space-y-4"
        >
          <StorageUsageMeter
            :usage="usage"
            :loading="false"
          />

          <RepositoryByteOverridePanel
            :eligibility="byteOverrideEligibility"
            :loading="byteOverrideLoading"
            :saving="byteOverrideSaving"
            :error="byteOverrideError"
            :success="byteOverrideSuccess"
            @save="saveByteOverride"
          />
        </section>

        <section
          id="danger"
          class="scroll-mt-24"
        >
          <UCard>
            <template #header>
              <h2 class="font-semibold text-error">
                {{ t('repo.settings.dangerZone') }}
              </h2>
            </template>
            <p class="text-sm text-[var(--ogb-text-muted)]">
              {{ t('repo.settings.deleteWarning') }}
            </p>
            <UButton
              color="error"
              variant="soft"
              class="mt-4"
              @click="showDeleteConfirm = true"
            >
              {{ t('repo.settings.deleteButton') }}
            </UButton>
          </UCard>
        </section>
      </div>
    </div>

    <UModal v-model:open="showDeleteConfirm">
      <template #content>
        <UCard>
          <template #header>
            <h3 class="font-semibold">
              {{ t('repo.settings.deleteConfirmTitle') }}
            </h3>
          </template>
          <p class="text-sm text-[var(--ogb-text-muted)]">
            {{ t('repo.settings.deleteConfirmDescription') }}
          </p>
          <div class="mt-4 flex justify-end gap-2">
            <UButton
              variant="ghost"
              @click="showDeleteConfirm = false"
            >
              {{ t('common.cancel') }}
            </UButton>
            <UButton
              color="error"
              :loading="deleting"
              @click="deleteRepo"
            >
              {{ t('repo.settings.deleteButton') }}
            </UButton>
          </div>
        </UCard>
      </template>
    </UModal>
  </div>
</template>
