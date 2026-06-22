<script setup lang="ts">
definePageMeta({
  layout: false,
})

const { instanceName, instanceLogoUrl } = useInstanceBranding()
const { t } = useI18n()
const colorMode = useColorMode()

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
  </div>
</template>
