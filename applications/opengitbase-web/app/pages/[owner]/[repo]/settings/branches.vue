<script setup lang="ts">
import type { ProtectedBranchRule, RepositoryMember } from '~/utils/api'

definePageMeta({ middleware: 'auth' })

const route = useRoute()
const { t } = useI18n()
const api = useApi()

const owner = computed(() => String(route.params.owner))
const repoSlug = computed(() => String(route.params.repo))

const refs = ref<string[]>([])
const members = ref<RepositoryMember[]>([])
const defaultBranchName = ref('')
const rules = ref<ProtectedBranchRule[]>([])
const loading = ref(true)
const savingDefault = ref(false)
const savingRule = ref(false)
const editingRuleId = ref<string | null>(null)
const error = ref<string | null>(null)

const pushRoleOptions = [
  { label: 'Owner', value: 'Owner' },
  { label: 'Maintainer', value: 'Admin' },
  { label: 'Writer', value: 'Writer' },
]

const mergeRoleOptions = [
  { label: t('repo.members.roles.writer'), value: 'Writer' },
  { label: t('repo.mergeRequests.maintainer'), value: 'Admin' },
  { label: t('org.members.roles.owner'), value: 'Owner' },
]

const forcePushOptions = [
  { label: t('repo.mergeRequests.forcePushDenyAll'), value: 'DenyAll' },
  { label: t('repo.mergeRequests.forcePushAllowList'), value: 'AllowAllowedPushers' },
  { label: t('repo.mergeRequests.forcePushPlatformOnly'), value: 'PlatformOnly' },
]

const mergeStrategyOptions = [
  { label: t('repo.mergeRequests.mergeStrategyAny'), value: null },
  { label: t('repo.mergeRequests.mergeStrategyMergeCommit'), value: 'MergeCommit' },
  { label: t('repo.mergeRequests.mergeStrategySquash'), value: 'Squash' },
  { label: t('repo.mergeRequests.mergeStrategyFastForward'), value: 'FastForward' },
]

function emptyRule(): Partial<ProtectedBranchRule> {
  return {
    pattern: '',
    blockDirectPush: true,
    allowedPushRoles: ['Admin'],
    allowedPushUserIds: [],
    requiredApprovals: 1,
    mergeRole: 'Writer',
    forcePushPolicy: 'DenyAll',
    dismissApprovalsOnPush: true,
    lockedMergeStrategy: null,
    pushRules: {
      maxFileSizeBytes: null,
      forbiddenPathGlobs: [],
      commitMessageRegex: '',
      requireDcoSignoff: false,
    },
  }
}

const ruleForm = reactive<Partial<ProtectedBranchRule>>(emptyRule())
const forbiddenGlobsText = ref('')

function resetRuleForm(): void {
  Object.assign(ruleForm, emptyRule())
  forbiddenGlobsText.value = ''
  editingRuleId.value = null
}

function populateRuleForm(rule: ProtectedBranchRule): void {
  Object.assign(ruleForm, {
    ...rule,
    pushRules: {
      maxFileSizeBytes: rule.pushRules.maxFileSizeBytes ?? null,
      forbiddenPathGlobs: rule.pushRules.forbiddenPathGlobs ?? [],
      commitMessageRegex: rule.pushRules.commitMessageRegex ?? '',
      requireDcoSignoff: rule.pushRules.requireDcoSignoff ?? false,
    },
  })
  forbiddenGlobsText.value = (rule.pushRules.forbiddenPathGlobs ?? []).join('\n')
  editingRuleId.value = rule.id
}

function buildRulePayload(): Partial<ProtectedBranchRule> {
  const globs = forbiddenGlobsText.value
    .split('\n')
    .map(line => line.trim())
    .filter(Boolean)
  return {
    ...ruleForm,
    pushRules: {
      maxFileSizeBytes: ruleForm.pushRules?.maxFileSizeBytes ?? null,
      forbiddenPathGlobs: globs,
      commitMessageRegex: ruleForm.pushRules?.commitMessageRegex?.trim() || null,
      requireDcoSignoff: ruleForm.pushRules?.requireDcoSignoff ?? false,
    },
  }
}

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  const repoResult = await api.repositories.getBySlug(owner.value, repoSlug.value)
  const repoId = repoResult.data?.id
  const [refsResult, defaultBranchResult, rulesResult, membersResult] = await Promise.all([
    api.repositoryContent.getRefs(owner.value, repoSlug.value),
    api.repositorySettings.getDefaultBranch(owner.value, repoSlug.value),
    api.repositorySettings.listProtectedBranchRules(owner.value, repoSlug.value),
    repoId ? api.repositoryMembers.list(repoId) : Promise.resolve({ data: [], error: null, status: 200 }),
  ])
  refs.value = refsResult.data?.branches.map(branch => branch.name) ?? []
  defaultBranchName.value = defaultBranchResult.data?.defaultBranchName ?? refs.value[0] ?? ''
  rules.value = rulesResult.data ?? []
  members.value = membersResult.data ?? []
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

async function saveRule(): Promise<void> {
  if (!ruleForm.pattern?.trim()) {
    error.value = t('repo.mergeRequests.patternRequired')
    return
  }
  if ((ruleForm.pushRules?.maxFileSizeBytes ?? 0) < 0) {
    error.value = t('repo.mergeRequests.positiveFileSize')
    return
  }
  savingRule.value = true
  error.value = null
  const payload = buildRulePayload()
  const result = editingRuleId.value
    ? await api.repositorySettings.updateProtectedBranchRule(owner.value, repoSlug.value, editingRuleId.value, payload)
    : await api.repositorySettings.createProtectedBranchRule(owner.value, repoSlug.value, payload)
  savingRule.value = false
  if (result.error) {
    error.value = result.error
    return
  }
  resetRuleForm()
  await load()
}

async function deleteRule(ruleId: string): Promise<void> {
  await api.repositorySettings.deleteProtectedBranchRule(owner.value, repoSlug.value, ruleId)
  if (editingRuleId.value === ruleId) {
    resetRuleForm()
  }
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
    <p class="text-sm text-[var(--ogb-text-muted)]">
      {{ t('repo.mergeRequests.branchesDescription') }}
    </p>

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
        <div class="space-y-3">
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
            <div class="flex items-start justify-between gap-2">
              <div>
                <p class="font-mono text-sm">
                  {{ rule.pattern }}
                </p>
                <p class="mt-1 text-xs text-[var(--ogb-text-muted)]">
                  {{ t('repo.mergeRequests.requiredApprovals', { count: rule.requiredApprovals }) }} ·
                  {{ t('repo.mergeRequests.mergeRole', { role: rule.mergeRole === 'Admin' ? t('repo.mergeRequests.maintainer') : rule.mergeRole }) }}
                </p>
                <p
                  v-if="rule.allowedPushRoles.length"
                  class="mt-1 text-xs text-[var(--ogb-text-muted)]"
                >
                  {{ t('repo.mergeRequests.allowedPushRoles', { roles: rule.allowedPushRoles.join(', ') }) }}
                </p>
              </div>
              <div class="flex items-center gap-1">
                <UButton
                  variant="ghost"
                  size="xs"
                  icon="i-lucide-pencil"
                  @click="populateRuleForm(rule)"
                />
                <UButton
                  color="error"
                  variant="ghost"
                  size="xs"
                  icon="i-lucide-trash-2"
                  @click="deleteRule(rule.id)"
                />
              </div>
            </div>
          </div>

          <div
            class="rounded border p-3"
            style="border-color: var(--ogb-border);"
          >
            <p class="mb-2 text-sm font-medium">
              {{ editingRuleId ? t('repo.mergeRequests.editRule') : t('repo.mergeRequests.addRule') }}
            </p>
            <div class="grid gap-3 sm:grid-cols-2">
              <div class="sm:col-span-2">
                <UInput
                  v-model="ruleForm.pattern"
                  :placeholder="t('repo.mergeRequests.patternPlaceholder')"
                />
                <p class="mt-1 text-xs text-[var(--ogb-text-muted)]">
                  {{ t('repo.mergeRequests.patternHelper') }}
                </p>
              </div>
              <UInputNumber
                v-model="ruleForm.requiredApprovals"
                :min="0"
                :placeholder="t('repo.mergeRequests.requiredApprovalsLabel')"
              />
              <USelect
                v-model="ruleForm.mergeRole"
                :items="mergeRoleOptions"
                value-key="value"
                label-key="label"
              />
              <USelectMenu
                v-model="ruleForm.allowedPushRoles"
                :items="pushRoleOptions"
                value-key="value"
                label-key="label"
                multiple
                :placeholder="t('repo.mergeRequests.allowedPushRolesLabel')"
              />
              <USelectMenu
                v-model="ruleForm.allowedPushUserIds"
                :items="members.map(member => ({ label: member.username ?? member.userId, value: member.userId }))"
                value-key="value"
                label-key="label"
                multiple
                :placeholder="t('repo.mergeRequests.allowedPushMembersLabel')"
              />
              <USelect
                v-model="ruleForm.forcePushPolicy"
                :items="forcePushOptions"
                value-key="value"
                label-key="label"
              />
              <USelect
                v-model="ruleForm.lockedMergeStrategy"
                :items="mergeStrategyOptions"
                value-key="value"
                label-key="label"
              />
              <UInputNumber
                v-model="ruleForm.pushRules!.maxFileSizeBytes"
                :placeholder="t('repo.mergeRequests.maxFileSize')"
                :min="0"
              />
              <UInput
                v-model="ruleForm.pushRules!.commitMessageRegex"
                :placeholder="t('repo.mergeRequests.commitRegex')"
              />
              <UTextarea
                v-model="forbiddenGlobsText"
                class="sm:col-span-2"
                :placeholder="t('repo.mergeRequests.forbiddenGlobs')"
                :rows="2"
              />
              <UCheckbox
                v-model="ruleForm.pushRules!.requireDcoSignoff"
                :label="t('repo.mergeRequests.requireDco')"
              />
              <UCheckbox
                v-model="ruleForm.dismissApprovalsOnPush"
                :label="t('repo.mergeRequests.dismissOnPush')"
              />
              <UCheckbox
                v-model="ruleForm.blockDirectPush"
                :label="t('repo.mergeRequests.blockDirectPush')"
              />
            </div>
            <div class="mt-3 flex gap-2">
              <UButton
                :loading="savingRule"
                @click="saveRule"
              >
                {{ editingRuleId ? t('common.save') : t('repo.mergeRequests.addRule') }}
              </UButton>
              <UButton
                v-if="editingRuleId"
                variant="ghost"
                @click="resetRuleForm"
              >
                {{ t('common.cancel') }}
              </UButton>
            </div>
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
