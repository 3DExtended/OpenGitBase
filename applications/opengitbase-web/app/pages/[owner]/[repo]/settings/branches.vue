<script setup lang="ts">
import type { ProtectedBranchRule } from '~/utils/api'

definePageMeta({ middleware: 'auth' })

const route = useRoute()
const { t } = useI18n()
const api = useApi()

const owner = computed(() => String(route.params.owner))
const repoSlug = computed(() => String(route.params.repo))

const refs = ref<string[]>([])
const defaultBranchName = ref('')
const rules = ref<ProtectedBranchRule[]>([])
const loading = ref(true)
const savingDefault = ref(false)
const error = ref<string | null>(null)
const newRule = reactive<Partial<ProtectedBranchRule>>({
  pattern: '',
  blockDirectPush: true,
  allowedPushRoles: ['Writer'],
  requiredApprovals: 1,
  mergeRole: 'Writer',
  forcePushPolicy: 'Deny',
  dismissApprovalsOnPush: true,
  pushRules: {
    maxFileSizeBytes: null,
    forbiddenPathGlobs: [],
    commitMessageRegex: null,
    requireDcoSignoff: false,
  },
})

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  const [refsResult, defaultBranchResult, rulesResult] = await Promise.all([
    api.repositoryContent.getRefs(owner.value, repoSlug.value),
    api.repositorySettings.getDefaultBranch(owner.value, repoSlug.value),
    api.repositorySettings.listProtectedBranchRules(owner.value, repoSlug.value),
  ])
  refs.value = refsResult.data?.branches.map(branch => branch.name) ?? []
  defaultBranchName.value = defaultBranchResult.data?.defaultBranchName ?? refs.value[0] ?? ''
  rules.value = rulesResult.data ?? []
  loading.value = false
}

async function saveDefaultBranch(): Promise<void> {
  if (!defaultBranchName.value) {
    return
  }
  savingDefault.value = true
  const result = await api.repositorySettings.updateDefaultBranch(owner.value, repoSlug.value, {
    defaultBranchName: defaultBranchName.value,
  })
  savingDefault.value = false
  if (result.error) {
    error.value = result.error
  }
}

async function createRule(): Promise<void> {
  if (!newRule.pattern?.trim()) {
    error.value = t('repo.mergeRequests.patternRequired')
    return
  }
  if ((newRule.pushRules?.maxFileSizeBytes ?? 0) < 0) {
    error.value = t('repo.mergeRequests.positiveFileSize')
    return
  }
  const result = await api.repositorySettings.createProtectedBranchRule(owner.value, repoSlug.value, newRule)
  if (result.error) {
    error.value = result.error
    return
  }
  await load()
  newRule.pattern = ''
}

async function deleteRule(ruleId: string): Promise<void> {
  await api.repositorySettings.deleteProtectedBranchRule(owner.value, repoSlug.value, ruleId)
  await load()
}

onMounted(() => {
  void load()
})
</script>

<template>
  <div class="mx-auto max-w-4xl space-y-4">
    <UButton
      :to="`/${owner}/${repoSlug}/settings`"
      variant="ghost"
      icon="i-lucide-arrow-left"
      size="sm"
    >
      {{ t('repo.settings.title') }}
    </UButton>

    <h1 class="text-2xl font-semibold">
      {{ t('repo.mergeRequests.branchesTitle') }}
    </h1>

    <UCard v-if="loading">
      <p class="text-sm text-[var(--ogb-text-muted)]">
        {{ t('common.loading') }}
      </p>
    </UCard>

    <template v-else>
      <UCard>
        <template #header>
          <h2 class="font-semibold">
            {{ t('repo.mergeRequests.defaultBranch') }}
          </h2>
        </template>
        <div class="flex flex-wrap items-end gap-3">
          <USelect
            v-model="defaultBranchName"
            :items="refs"
            class="min-w-52"
          />
          <UButton
            :loading="savingDefault"
            @click="saveDefaultBranch"
          >
            {{ t('common.save') }}
          </UButton>
        </div>
      </UCard>

      <UCard>
        <template #header>
          <h2 class="font-semibold">
            {{ t('repo.mergeRequests.protectedRules') }}
          </h2>
        </template>
        <div class="space-y-2">
          <p
            v-if="!rules.length"
            class="text-sm text-[var(--ogb-text-muted)]"
          >
            {{ t('repo.mergeRequests.noRules') }}
          </p>
          <div
            v-for="rule in rules"
            :key="rule.id"
            class="rounded border p-3"
            style="border-color: var(--ogb-border);"
          >
            <div class="flex items-center justify-between gap-2">
              <div>
                <p class="font-mono text-sm">
                  {{ rule.pattern }}
                </p>
                <p class="text-xs text-[var(--ogb-text-muted)]">
                  {{ t('repo.mergeRequests.requiredApprovals', { count: rule.requiredApprovals }) }} ·
                  {{ t('repo.mergeRequests.mergeRole', { role: rule.mergeRole === 'Admin' ? t('repo.mergeRequests.maintainer') : rule.mergeRole }) }}
                </p>
              </div>
              <UButton
                color="error"
                variant="ghost"
                size="xs"
                icon="i-lucide-trash-2"
                @click="deleteRule(rule.id)"
              />
            </div>
          </div>

          <div class="rounded border p-3" style="border-color: var(--ogb-border);">
            <p class="mb-2 text-sm font-medium">
              {{ t('repo.mergeRequests.addRule') }}
            </p>
            <div class="grid gap-3 sm:grid-cols-2">
              <UInput
                v-model="newRule.pattern"
                :placeholder="t('repo.mergeRequests.patternPlaceholder')"
              />
              <UInputNumber
                v-model="newRule.requiredApprovals"
                :min="0"
              />
              <USelect
                v-model="newRule.mergeRole"
                :items="[
                  { label: t('repo.members.roles.writer'), value: 'Writer' },
                  { label: t('repo.mergeRequests.maintainer'), value: 'Admin' },
                  { label: t('org.members.roles.owner'), value: 'Owner' },
                ]"
                value-key="value"
                label-key="label"
              />
              <UInputNumber
                v-model="newRule.pushRules!.maxFileSizeBytes"
                :placeholder="t('repo.mergeRequests.maxFileSize')"
                :min="0"
              />
              <UInput
                v-model="newRule.pushRules!.commitMessageRegex"
                :placeholder="t('repo.mergeRequests.commitRegex')"
              />
              <UCheckbox
                v-model="newRule.pushRules!.requireDcoSignoff"
                :label="t('repo.mergeRequests.requireDco')"
              />
              <UCheckbox
                v-model="newRule.dismissApprovalsOnPush"
                :label="t('repo.mergeRequests.dismissOnPush')"
              />
              <UCheckbox
                v-model="newRule.blockDirectPush"
                :label="t('repo.mergeRequests.blockDirectPush')"
              />
            </div>
            <UButton class="mt-3" @click="createRule">
              {{ t('repo.mergeRequests.addRule') }}
            </UButton>
          </div>
        </div>
      </UCard>
    </template>

    <UAlert
      v-if="error"
      color="error"
      variant="subtle"
      :description="error"
    />
  </div>
</template>
