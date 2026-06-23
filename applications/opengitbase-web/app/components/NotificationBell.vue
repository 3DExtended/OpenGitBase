<script setup lang="ts">
import type { Notification } from '~/utils/api'
import { discussionDetailRoute } from '~/utils/discussionPaths'

const { t } = useI18n()
const api = useApi()

const open = ref(false)
const notifications = ref<Notification[]>([])
const loading = ref(false)

const unreadCount = computed(() =>
  notifications.value.filter(n => !n.isRead).length,
)

async function loadNotifications(): Promise<void> {
  loading.value = true
  const result = await api.discussions.notifications.list()
  notifications.value = result.data ?? []
  loading.value = false
}

async function markRead(notification: Notification): Promise<void> {
  if (notification.isRead) {
    return
  }
  await api.discussions.notifications.markRead(notification.id)
  notification.readAt = new Date().toISOString()
  notification.isRead = true
}

async function openNotification(notification: Notification): Promise<void> {
  open.value = false
  void markRead(notification)
  const target = discussionDetailRoute(
    notification.ownerSlug,
    notification.repositorySlug,
    notification.discussionNumber,
    notification.commentId ? { commentId: notification.commentId } : undefined,
  )
  if (target) {
    await navigateTo(target)
  }
}

watch(open, (isOpen) => {
  if (isOpen) {
    void loadNotifications()
  }
})

onMounted(() => {
  void loadNotifications()
})
</script>

<template>
  <UPopover v-model:open="open">
    <UButton
      color="neutral"
      variant="ghost"
      icon="i-lucide-bell"
      class="relative"
      :aria-label="t('notifications.title')"
    >
      <UBadge
        v-if="unreadCount"
        color="error"
        variant="solid"
        size="sm"
        class="absolute -right-0.5 -top-0.5 min-w-4 justify-center px-1"
      >
        {{ unreadCount > 9 ? '9+' : unreadCount }}
      </UBadge>
    </UButton>

    <template #content>
      <div class="w-80 p-2">
        <p class="px-2 py-1 text-sm font-semibold">
          {{ t('notifications.title') }}
        </p>

        <div
          v-if="loading"
          class="px-2 py-4 text-sm text-[var(--ogb-text-muted)]"
        >
          {{ t('common.loading') }}
        </div>

        <p
          v-else-if="!notifications.length"
          class="px-2 py-4 text-sm text-[var(--ogb-text-muted)]"
        >
          {{ t('notifications.empty') }}
        </p>

        <ul
          v-else
          class="max-h-80 divide-y overflow-y-auto"
          style="border-color: var(--ogb-border);"
        >
          <li
            v-for="notification in notifications"
            :key="notification.id"
          >
            <button
              type="button"
              class="block w-full rounded-md px-2 py-2 text-left text-sm transition-colors hover:bg-[var(--ogb-bg)]"
              :class="{ 'bg-[var(--ogb-bg)]': !notification.isRead }"
              @click="openNotification(notification)"
            >
              <p class="line-clamp-2">
                {{ notification.message }}
              </p>
              <p class="mt-1 text-xs text-[var(--ogb-text-muted)]">
                {{ notification.ownerSlug }}/{{ notification.repositorySlug }} #{{ notification.discussionNumber }}
                · {{ new Date(notification.createdAt).toLocaleString() }}
              </p>
            </button>
          </li>
        </ul>
      </div>
    </template>
  </UPopover>
</template>
