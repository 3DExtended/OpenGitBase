<script setup lang="ts">
definePageMeta({ middleware: 'admin' })

const { t } = useI18n()

useHead({ title: t('admin.title') })

const sections = computed(() => [
  {
    title: t('admin.storage.title'),
    description: t('admin.storage.description'),
    to: '/admin/storage',
    icon: 'i-lucide-server',
    available: true,
  },
  {
    title: t('admin.compute.title'),
    description: t('admin.compute.description'),
    to: '/admin/compute',
    icon: 'i-lucide-cpu',
    available: true,
  },
  {
    title: t('admin.status.title'),
    description: t('admin.status.description'),
    to: '/admin/status',
    icon: 'i-lucide-activity',
    available: true,
  },
  {
    title: t('admin.replication.title'),
    description: t('admin.replication.description'),
    to: '/admin/repositories',
    icon: 'i-lucide-database-backup',
    available: true,
  },
])
</script>

<template>
  <div class="mx-auto max-w-3xl space-y-6">
    <div>
      <h1 class="text-2xl font-semibold">
        {{ t('admin.title') }}
      </h1>
      <p class="mt-1 text-sm text-[var(--ogb-text-muted)]">
        {{ t('admin.subtitle') }}
      </p>
    </div>

    <div class="grid gap-4 sm:grid-cols-2">
      <UCard
        v-for="section in sections"
        :key="section.to"
        class="transition-colors hover:border-[var(--ogb-accent)]"
      >
        <div class="flex items-start gap-3">
          <div
            class="flex size-10 shrink-0 items-center justify-center rounded-lg"
            style="background-color: color-mix(in srgb, var(--ogb-accent) 12%, transparent);"
          >
            <UIcon
              :name="section.icon"
              class="size-5 text-[var(--ogb-accent)]"
            />
          </div>
          <div class="min-w-0 flex-1">
            <h2 class="font-semibold">
              {{ section.title }}
            </h2>
            <p class="mt-1 text-sm text-[var(--ogb-text-muted)]">
              {{ section.description }}
            </p>
            <UButton
              :to="section.to"
              variant="soft"
              size="sm"
              class="mt-3"
              trailing-icon="i-lucide-arrow-right"
            >
              {{ t('admin.open') }}
            </UButton>
          </div>
        </div>
      </UCard>
    </div>
  </div>
</template>
