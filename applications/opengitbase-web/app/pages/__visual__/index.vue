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

useHead({ title: 'Visual Gallery' })

function toggleTheme() {
  colorMode.preference = colorMode.value === 'dark' ? 'light' : 'dark'
}
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
      data-testid="visual-org-storage-settings"
    >
      <h2 class="text-sm font-medium uppercase tracking-wider text-[var(--ogb-text-muted)]">
        Org storage settings
      </h2>
      <UCard>
        <template #header>
          <h3 class="font-semibold">
            Quota credits
          </h3>
        </template>
        <dl class="grid gap-3 text-sm sm:grid-cols-3">
          <div>
            <dt class="text-[var(--ogb-text-muted)]">
              Platform limit
            </dt>
            <dd>1.00 GB</dd>
          </div>
          <div>
            <dt class="text-[var(--ogb-text-muted)]">
              Contributed capacity
            </dt>
            <dd>2.00 GB</dd>
          </div>
          <div>
            <dt class="text-[var(--ogb-text-muted)]">
              Effective limit
            </dt>
            <dd>3.00 GB</dd>
          </div>
        </dl>
      </UCard>
      <UCard>
        <template #header>
          <h3 class="font-semibold">
            Placement defaults
          </h3>
        </template>
        <div class="grid gap-3 text-sm">
          <div>Default placement: Max self-host</div>
          <div>Self-host preference: Prefer self-host</div>
        </div>
      </UCard>
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
