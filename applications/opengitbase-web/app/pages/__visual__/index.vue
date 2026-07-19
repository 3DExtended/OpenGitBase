<script setup lang="ts">
definePageMeta({
  layout: false,
  middleware: 'visual-dev-only',
})

const { instanceName, instanceLogoUrl } = useInstanceBranding()
const { t } = useI18n()
const colorMode = useColorMode()
import { visualDiscussionSubThreads } from '~/components/discussions/discussionSubThreadVisualFixtures'

function memberLabel(userId: string): string {
  return userId === 'user-1' ? 'alice' : 'bob'
}

const sampleOutageWindows = [
  {
    id: 'window-open',
    scope: 0,
    group: 6,
    instanceId: null,
    displayName: 'Message bus',
    startedAt: '2026-07-19T10:00:00Z',
    endedAt: null,
    isOpen: true,
    isPartial: false,
    durationMinutes: 65,
    suppressed: false,
    annotation: null,
  },
  {
    id: 'window-closed-annotated',
    scope: 0,
    group: 3,
    instanceId: null,
    displayName: 'Git',
    startedAt: '2026-07-18T08:00:00Z',
    endedAt: '2026-07-18T08:40:00Z',
    isOpen: false,
    isPartial: false,
    durationMinutes: 40,
    suppressed: false,
    annotation: 'Scheduled failover drill.',
  },
  {
    id: 'window-suppressed',
    scope: 1,
    group: 5,
    instanceId: 'redis',
    displayName: 'Redis',
    startedAt: '2026-07-17T22:00:00Z',
    endedAt: '2026-07-17T22:12:00Z',
    isOpen: false,
    isPartial: true,
    durationMinutes: 12,
    suppressed: true,
    annotation: null,
  },
]

useHead({ title: 'Visual Gallery' })

function toggleTheme() {
  colorMode.preference = colorMode.value === 'dark' ? 'light' : 'dark'
}
const sampleStorageSettings = {
  organizationId: 'org-1',
  defaultPlacementPolicy: 2,
  defaultSelfHostPreference: 1,
  platformBytesLimit: 1_073_741_824,
  contributedBytesCapacity: 2_147_483_648,
  bytesLimit: 3_221_225_472,
}

const sampleHealthyNode = {
  id: 'node-healthy',
  nodeId: 'org-storage-1',
  internalHost: 'storage-1.example.com',
  internalHttpPort: 8081,
  isHealthy: true,
  maxBytes: 1_099_511_627_776,
  usedBytes: 5_368_709_120,
  hostingScope: 0,
}

const sampleUnhealthyNode = {
  id: 'node-unhealthy',
  nodeId: 'org-storage-2',
  internalHost: 'storage-2.example.com',
  internalHttpPort: 8081,
  isHealthy: false,
  maxBytes: 549_755_813_888,
  usedBytes: 12_884_901_888,
  hostingScope: 1,
}

const sampleEnrollment = {
  id: 'enrollment-1',
  nodeId: 'org-storage-3',
  createdAt: '2026-07-12T10:00:00Z',
  expiresAt: '2026-07-19T10:00:00Z',
  consumedAt: null,
}

const sampleBootstrapCommand = `curl -fsSL https://raw.githubusercontent.com/3DExtended/OpenGitBase/main/scripts/bootstrap-org-storage-node.sh | bash -s -- \\
  --token "example-token" \\
  --node-id "org-storage-3" \\
  --api-url "https://api.example.com/api" \\
  --internal-host "storage.example.com"`

const placementOptions = [
  { label: 'Inherit platform default', value: 0 },
  { label: 'Platform default', value: 1 },
  { label: 'Max self-host', value: 2 },
]

const selfHostOptions = [
  { label: 'Platform only', value: 0 },
  { label: 'Prefer self-host', value: 1 },
  { label: 'Require self-host', value: 2 },
]
</script>

<template>
  <div
    class="min-h-dvh p-6"
    style="background-color: var(--ogb-bg);"
    data-testid="visual-gallery"
  >
    <h1 class="mb-8 text-2xl font-semibold">
      Component Gallery
    </h1>

    <section
      class="mb-10 space-y-4"
      data-testid="visual-header"
    >
      <h2 class="text-sm font-medium uppercase tracking-wider text-[var(--ogb-text-muted)]">
        App Header
      </h2>
      <AppHeader
        :instance-name="instanceName"
        :instance-logo-url="instanceLogoUrl"
      />
    </section>

    <section
      class="mb-10"
      data-testid="visual-sidebar"
    >
      <h2 class="mb-4 text-sm font-medium uppercase tracking-wider text-[var(--ogb-text-muted)]">
        App Sidebar
      </h2>
      <div
        class="flex h-[28rem] border"
        style="border-color: var(--ogb-border);"
      >
        <aside
          class="w-[var(--ogb-sidebar-width)] shrink-0 border-r"
          style="border-color: var(--ogb-border); background-color: var(--ogb-surface);"
        >
          <AppSidebarPanel :expanded="true" />
        </aside>
        <div class="flex flex-1 items-center justify-center text-sm text-[var(--ogb-text-muted)]">
          Main content area
        </div>
      </div>
    </section>

    <section
      class="mb-10 grid max-w-md gap-4"
      data-testid="visual-buttons"
    >
      <h2 class="text-sm font-medium uppercase tracking-wider text-[var(--ogb-text-muted)]">
        Buttons
      </h2>
      <UButton>Primary</UButton>
      <UButton variant="soft">
        Soft
      </UButton>
      <UButton
        variant="ghost"
        @click="toggleTheme"
      >
        Toggle theme
      </UButton>
    </section>

    <section
      class="mb-10 max-w-md space-y-4"
      data-testid="visual-cli-auth"
    >
      <h2 class="text-sm font-medium uppercase tracking-wider text-[var(--ogb-text-muted)]">
        CLI auth
      </h2>
      <UCard>
        <template #header>
          <h3 class="font-semibold">
            Sign in for CLI
          </h3>
          <p class="mt-1 text-sm text-[var(--ogb-text-muted)]">
            Authenticating against <span class="font-medium">{{ instanceName }}</span>
          </p>
        </template>
        <UFormField :label="t('auth.fields.username')">
          <UInput model-value="demo-user" />
        </UFormField>
        <UFormField
          :label="t('auth.fields.password')"
          class="mt-3"
        >
          <UInput
            model-value="••••••••"
            type="password"
          />
        </UFormField>
        <UButton
          block
          class="mt-4"
        >
          {{ t('auth.signIn.submit') }}
        </UButton>
      </UCard>
    </section>

    <section
      class="mb-10 max-w-md space-y-4"
      data-testid="visual-auth-card"
    >
      <h2 class="text-sm font-medium uppercase tracking-wider text-[var(--ogb-text-muted)]">
        Auth card
      </h2>
      <UCard>
        <template #header>
          <h3 class="font-semibold">
            {{ t('auth.signIn.title') }}
          </h3>
        </template>
        <UFormField :label="t('auth.fields.username')">
          <UInput model-value="demo-user" />
        </UFormField>
        <UFormField
          :label="t('auth.fields.password')"
          class="mt-3"
        >
          <UInput
            model-value="••••••••"
            type="password"
          />
        </UFormField>
        <UButton
          block
          class="mt-4"
        >
          {{ t('auth.signIn.submit') }}
        </UButton>
      </UCard>
    </section>

    <section
      class="mb-10 max-w-md"
      data-testid="visual-verification-banner"
    >
      <h2 class="mb-4 text-sm font-medium uppercase tracking-wider text-[var(--ogb-text-muted)]">
        Email verification banner
      </h2>
      <UAlert
        color="warning"
        variant="subtle"
        icon="i-lucide-mail-warning"
        :title="t('verification.bannerTitle')"
        :description="t('verification.bannerDescription')"
      />
    </section>

    <section
      class="max-w-md"
      data-testid="visual-storage-meter-low"
    >
      <h2 class="mb-4 text-sm font-medium uppercase tracking-wider text-[var(--ogb-text-muted)]">
        Storage meter (low usage)
      </h2>
      <StorageUsageMeter
        :usage="{ bytesUsed: 2726298, bytesLimit: 1073741824, fileSizeLimit: 52428800 }"
      />
    </section>

    <section
      class="mt-10 max-w-md"
      data-testid="visual-storage-meter-normal"
    >
      <h2 class="mb-4 text-sm font-medium uppercase tracking-wider text-[var(--ogb-text-muted)]">
        Storage meter (normal)
      </h2>
      <StorageUsageMeter
        :usage="{ bytesUsed: 524288000, bytesLimit: 1073741824, fileSizeLimit: 52428800 }"
      />
    </section>

    <section
      class="mt-10 max-w-md"
      data-testid="visual-storage-meter-warning"
    >
      <h2 class="mb-4 text-sm font-medium uppercase tracking-wider text-[var(--ogb-text-muted)]">
        Storage meter (warning)
      </h2>
      <StorageUsageMeter
        :usage="{ bytesUsed: 900000000, bytesLimit: 1073741824, fileSizeLimit: 52428800 }"
      />
    </section>

    <section
      class="mt-10 max-w-3xl space-y-4"
      data-testid="visual-discussion-sub-threads"
    >
      <h2 class="text-sm font-medium uppercase tracking-wider text-[var(--ogb-text-muted)]">
        Discussion sub-threads
      </h2>
      <DiscussionSubThread
        :comment="visualDiscussionSubThreads.open as any"
        owner="acme"
        repo-slug="demo"
        :member-label="memberLabel"
        :can-resolve="true"
        :can-reply="true"
      />
      <DiscussionSubThread
        :comment="visualDiscussionSubThreads.resolved as any"
        owner="acme"
        repo-slug="demo"
        :member-label="memberLabel"
        :can-resolve="true"
        :can-reply="true"
      />
      <DiscussionSubThread
        :comment="visualDiscussionSubThreads.orphan as any"
        owner="acme"
        repo-slug="demo"
        :member-label="memberLabel"
        :can-resolve="false"
        :can-reply="false"
      />
    </section>

    <section
      class="mt-10 max-w-3xl space-y-4"
      data-testid="visual-merge-requests-overview"
    >
      <h2 class="text-sm font-medium uppercase tracking-wider text-[var(--ogb-text-muted)]">
        Merge request overview comments
      </h2>
      <CollaborationThread
        :thread="{
          id: 'mr-thread-1',
          author: { userId: 'user-1', username: 'alice' },
          bodyMarkdown: 'Please split this helper into a separate composable.',
          createdAt: '2026-06-27T08:00:00Z',
          isResolved: false,
          replyCount: 1,
          replies: [
            {
              id: 'mr-reply-1',
              author: { userId: 'user-2', username: 'bob' },
              bodyMarkdown: 'Done in the latest commit.',
              createdAt: '2026-06-27T08:20:00Z',
            },
          ],
          anchor: null,
        }"
        owner="acme"
        repo-slug="demo"
        :member-label="memberLabel"
        :can-resolve="true"
        :can-reply="true"
      />
    </section>

    <section
      class="mt-10 max-w-3xl space-y-4"
      data-testid="visual-commit-unified-diff"
    >
      <h2 class="text-sm font-medium uppercase tracking-wider text-[var(--ogb-text-muted)]">
        Commit unified diff
      </h2>
      <RepoUnifiedDiff
        :files="[
          {
            filePath: 'src/policy.ts',
            changeType: 'modified',
            hunks: [
              {
                header: '@@ -1,3 +1,4 @@',
                lines: [
                  { oldLineNumber: 1, newLineNumber: 1, type: 'context', content: 'export function matches(ref: string) {' },
                  { oldLineNumber: null, newLineNumber: 2, type: 'add', content: '  return ref.startsWith(\'release/\')' },
                  { oldLineNumber: 2, newLineNumber: 3, type: 'context', content: '}' },
                ],
              },
            ],
          },
        ]"
        read-only
      />
    </section>

    <section
      class="mt-10 max-w-4xl space-y-4"
      data-testid="visual-pipelines"
    >
      <h2 class="text-sm font-medium uppercase tracking-wider text-[var(--ogb-text-muted)]">
        Pipelines
      </h2>
      <UCard>
        <div class="space-y-3">
          <div class="flex items-center justify-between gap-3 rounded border p-3" style="border-color: var(--ogb-border);">
            <div>
              <p class="font-medium">refs/heads/main</p>
              <p class="font-mono text-xs text-[var(--ogb-text-muted)]">abc123def456</p>
            </div>
            <CollaborationStatusBadge label="Passed" color="success" />
          </div>
          <div class="rounded border p-3 font-mono text-xs" style="border-color: var(--ogb-border); background-color: var(--ogb-bg);">
            [workspace] Workspace prepared at /tmp/opengitbase-agent/run-1/repo
            <br>
            [script] Running test suite...
            <br>
            [script] All tests passed.
          </div>
        </div>
      </UCard>
    </section>

    <section
      class="mb-10 max-w-3xl space-y-4"
      data-testid="visual-org-compute"
    >
      <h2 class="text-sm font-medium uppercase tracking-wider text-[var(--ogb-text-muted)]">
        Organization compute settings
      </h2>
      <UCard>
        <template #header>
          <h3 class="font-semibold">
            Registered compute nodes
          </h3>
        </template>
        <div class="border-b py-3" style="border-color: var(--ogb-border);">
          <div class="flex items-center gap-2">
            <span class="font-medium">org-compute-1</span>
            <UBadge color="success" variant="subtle">Healthy</UBadge>
            <UBadge color="neutral" variant="subtle">Own org only</UBadge>
          </div>
          <p class="mt-1 text-xs text-[var(--ogb-text-muted)]">
            0/1 jobs · 1 vCPU · 2.00 GiB
          </p>
        </div>
      </UCard>
      <UCard>
        <template #header>
          <h3 class="font-semibold">
            Pending egress domain requests
          </h3>
        </template>
        <div
          class="rounded border p-3"
          style="border-color: var(--ogb-border);"
        >
          <p class="font-medium">
            registry.example.com
          </p>
          <p class="text-sm text-[var(--ogb-text-muted)]">
            Required for private package installs in org pipelines.
          </p>
          <div class="mt-2 flex gap-2">
            <UButton size="xs">
              Approve
            </UButton>
            <UButton
              size="xs"
              color="error"
              variant="soft"
            >
              Deny
            </UButton>
          </div>
        </div>
      </UCard>
    </section>

    <section
      class="mb-10 max-w-5xl space-y-4"
      data-testid="visual-admin-compute"
    >
      <h2 class="text-sm font-medium uppercase tracking-wider text-[var(--ogb-text-muted)]">
        Admin compute fleet
      </h2>
      <UCard>
        <template #header>
          <div class="flex items-center justify-between gap-3">
            <h3 class="font-semibold">
              Compute nodes
            </h3>
            <UBadge
              color="neutral"
              variant="subtle"
            >
              1/1 Healthy
            </UBadge>
          </div>
        </template>
        <UCard class="bg-[var(--ogb-bg)]">
          <div class="flex flex-wrap items-center gap-2">
            <span class="font-medium">compute-agent-1</span>
            <UBadge
              color="success"
              variant="subtle"
            >
              Healthy
            </UBadge>
          </div>
          <p class="mt-1 text-xs text-[var(--ogb-text-muted)]">
            Capacity: 0/2 jobs · 2 vCPU · 2.00 GiB
          </p>
        </UCard>
      </UCard>
      <UCard>
        <template #header>
          <h3 class="font-semibold">
            Compute enrollments
          </h3>
        </template>
        <div class="grid gap-3 md:grid-cols-4">
          <UFormField label="Node ID">
            <UInput model-value="compute-agent-1" />
          </UFormField>
          <UFormField label="Max concurrent jobs">
            <UInput model-value="2" />
          </UFormField>
          <UFormField label="Max vCPU">
            <UInput model-value="2" />
          </UFormField>
          <UFormField label="Max memory (GiB)">
            <UInput model-value="2" />
          </UFormField>
        </div>
        <UButton class="mt-3">
          Create enrollment
        </UButton>
      </UCard>
    </section>

    <section
      class="mt-10 max-w-3xl space-y-4"
      data-testid="visual-merge-request-banner"
    >
      <h2 class="text-sm font-medium uppercase tracking-wider text-[var(--ogb-text-muted)]">
        Post-push merge request banner
      </h2>
      <UAlert
        color="info"
        variant="subtle"
        icon="i-lucide-git-pull-request-create"
        title="Create a merge request"
        description="feature/refactor-auth is 3 commit(s) ahead of the default branch."
      >
        <template #actions>
          <UButton size="xs">
            Create merge request
          </UButton>
        </template>
      </UAlert>
    </section>

    <section
      class="mt-10 max-w-3xl space-y-4"
      data-testid="visual-branches-settings"
    >
      <h2 class="text-sm font-medium uppercase tracking-wider text-[var(--ogb-text-muted)]">
        Branches &amp; push rules settings
      </h2>
      <UCard>
        <template #header>
          <h3 class="font-semibold">
            Branches &amp; push rules
          </h3>
        </template>
        <div class="space-y-3">
          <div class="rounded border p-3" style="border-color: var(--ogb-border);">
            <p class="font-mono text-sm">
              @default
            </p>
            <p class="text-xs text-[var(--ogb-text-muted)]">
              2 required approvals · Merge role: Maintainer
            </p>
          </div>
          <div class="rounded border p-3" style="border-color: var(--ogb-border);">
            <p class="font-mono text-sm">
              release/*
            </p>
            <p class="text-xs text-[var(--ogb-text-muted)]">
              1 required approval · Merge role: Writer
            </p>
          </div>
        </div>
      </UCard>
    </section>

    <section
      class="mt-10 max-w-3xl space-y-4"
      data-testid="visual-repo-byte-override"
    >
      <h2 class="text-sm font-medium uppercase tracking-wider text-[var(--ogb-text-muted)]">
        Repository byte override
      </h2>
      <RepositoryByteOverridePanel
        :eligibility="{
          eligible: true,
          reason: 'Eligible for per-repository byte override.',
          currentOverride: 5368709120,
          maxAllowedOverride: 10737418240,
          orgContributedNodeCount: 5,
        }"
      />
      <RepositoryByteOverridePanel
        :eligibility="{
          eligible: false,
          reason: 'Organization must operate more than three healthy storage nodes.',
          currentOverride: null,
          maxAllowedOverride: 2147483648,
          orgContributedNodeCount: 3,
        }"
      />
    </section>

    <section
      class="mt-10 max-w-3xl space-y-4"
      data-testid="visual-org-storage-empty"
    >
      <h2 class="text-sm font-medium uppercase tracking-wider text-[var(--ogb-text-muted)]">
        Org storage — empty
      </h2>
      <OrgStorageQuotaCard :settings="sampleStorageSettings" />
      <OrgStorageNodeList :nodes="[]" />
      <OrgStorageEnrollmentSection
        :enrollments="[]"
        create-node-id="org-storage-1"
        :create-max-gi-b="100"
        :create-hosting-scope="0"
      />
    </section>

    <section
      class="mt-10 max-w-3xl space-y-4"
      data-testid="visual-org-storage-enrollment"
    >
      <h2 class="text-sm font-medium uppercase tracking-wider text-[var(--ogb-text-muted)]">
        Org storage — enrollment success
      </h2>
      <OrgStorageEnrollmentSection
        :enrollments="[sampleEnrollment]"
        create-node-id="org-storage-3"
        :create-max-gi-b="100"
        :create-hosting-scope="0"
        :bootstrap-command="sampleBootstrapCommand"
      />
    </section>

    <section
      class="mt-10 max-w-3xl space-y-4"
      data-testid="visual-org-storage-edit"
    >
      <h2 class="text-sm font-medium uppercase tracking-wider text-[var(--ogb-text-muted)]">
        Org storage — node edit open
      </h2>
      <OrgStorageNodeList
        :nodes="[sampleHealthyNode]"
        editing-node-id="node-healthy"
        :edit-max-gi-b="1024"
        :edit-hosting-scope="0"
      />
    </section>

    <section
      class="mt-10 max-w-3xl space-y-4"
      data-testid="visual-org-storage-unhealthy"
    >
      <h2 class="text-sm font-medium uppercase tracking-wider text-[var(--ogb-text-muted)]">
        Org storage — unhealthy node
      </h2>
      <OrgStorageNodeList :nodes="[sampleUnhealthyNode]" />
      <OrgStoragePlacementForm
        :placement-policy="2"
        :self-host-preference="1"
        :placement-options="placementOptions"
        :self-host-options="selfHostOptions"
      />
    </section>

    <section
      class="mt-10 max-w-2xl space-y-4"
      data-testid="visual-markdown-table"
    >
      <h2 class="text-sm font-medium uppercase tracking-wider text-[var(--ogb-text-muted)]">
        Markdown table
      </h2>
      <UCard>
        <RepoMarkdown
          :source="`## Identity model

| Credential | Lifetime | Purpose |
|------------|----------|---------|
| **Node Identity** | Long-lived | Enrollment, heartbeat, claim work |
| **Job Identity** | Per job | Read repo at SHA, logs, fetch base/layer blobs, report status |
`"
        />
      </UCard>
    </section>

    <section
      class="mt-10 max-w-3xl space-y-4"
      data-testid="visual-admin-outage-windows"
    >
      <h2 class="text-sm font-medium uppercase tracking-wider text-[var(--ogb-text-muted)]">
        Admin outage window controls
      </h2>
      <StatusAdminOutageWindowList :windows="sampleOutageWindows as any" />
    </section>

    <section
      class="mt-10 max-w-5xl space-y-4"
      data-testid="visual-admin-rf4-replication"
    >
      <h2 class="text-sm font-medium uppercase tracking-wider text-[var(--ogb-text-muted)]">
        Admin RF=4 replication detail
      </h2>
      <UCard>
        <div class="flex flex-wrap items-center gap-3">
          <AdminReplicationStateBadge state="Rf4Healthy" />
          <UBadge color="success" variant="subtle">
            Write quorum: yes
          </UBadge>
          <span class="text-sm text-[var(--ogb-text-muted)]">Primary watermark: 12</span>
          <span class="text-sm text-[var(--ogb-text-muted)]">Epoch: 3</span>
        </div>
      </UCard>
      <UCard>
        <template #header>
          <h3 class="font-semibold">Replicas</h3>
        </template>
        <div class="grid gap-4 md:grid-cols-3">
          <UCard class="border-[var(--ogb-accent)] bg-[var(--ogb-bg)]">
            <UBadge color="primary" variant="subtle">Primary</UBadge>
            <div class="mt-2 font-medium">storage-1</div>
            <div class="text-xs text-[var(--ogb-text-muted)]">Applied watermark: 12</div>
          </UCard>
          <div class="space-y-3 md:col-span-2">
            <UCard class="bg-[var(--ogb-bg)]">
              <UBadge color="neutral" variant="subtle">Read replica</UBadge>
              <div class="mt-1 font-medium">storage-2</div>
              <div class="text-xs text-[var(--ogb-text-muted)]">Applied watermark: 12</div>
            </UCard>
            <UCard class="bg-[var(--ogb-bg)]">
              <UBadge color="neutral" variant="subtle">Encrypted replica</UBadge>
              <div class="mt-1 font-medium">storage-3</div>
              <div class="text-xs text-[var(--ogb-text-muted)]">Artifact watermark: 12</div>
            </UCard>
            <UCard class="bg-[var(--ogb-bg)]">
              <UBadge color="neutral" variant="subtle">Encrypted replica</UBadge>
              <div class="mt-1 font-medium">storage-4</div>
              <div class="text-xs text-[var(--ogb-text-muted)]">Artifact watermark: 11</div>
              <UBadge class="mt-2" color="warning" variant="subtle">Lagging</UBadge>
            </UCard>
          </div>
        </div>
      </UCard>
    </section>
  </div>
</template>
