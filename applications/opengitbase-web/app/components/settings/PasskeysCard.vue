<script setup lang="ts">
import type { Passkey } from '~/utils/webauthn'
import { PASSKEY_NAME_MAX_LENGTH } from '~/utils/webauthn'

const props = defineProps<{
  passkeys: Passkey[]
  busy?: boolean
  error?: string | null
  /** Opens one passkey's name for editing; only the visual gallery uses it. */
  initialEditingId?: string
}>()

const emit = defineEmits<{
  add: [name: string]
  rename: [id: string, name: string]
  remove: [passkey: Passkey]
}>()

/** The add form is open; the page closes it once the passkey is stored. */
const adding = defineModel<boolean>('adding', { default: false })

const { t } = useI18n()
// Only fields the user opens get the focus; fields open from the start (gallery) must not steal it.
const openedByUser = ref(false)
const newName = ref('')
const editingId = ref<string | null>(props.initialEditingId ?? null)
const editingName = ref(props.passkeys.find(passkey => passkey.id === props.initialEditingId)?.name ?? '')

watch(adding, (open) => {
  if (open) {
    newName.value = ''
  }
})

function openAdd() {
  openedByUser.value = true
  adding.value = true
}

function startRename(passkey: Passkey) {
  openedByUser.value = true
  editingId.value = passkey.id
  editingName.value = passkey.name
}

function saveRename() {
  const name = editingName.value.trim()
  if (editingId.value && name) {
    emit('rename', editingId.value, name)
  }
  editingId.value = null
}

function submitAdd() {
  const name = newName.value.trim()
  if (name) {
    emit('add', name)
  }
}
</script>

<template>
  <UCard data-testid="passkeys-card">
    <template #header>
      <h2 class="font-semibold">
        {{ t('settings.security.passkeys.title') }}
      </h2>
    </template>

    <p class="text-sm text-[var(--ogb-text-muted)]">
      {{ t('settings.security.passkeys.description') }}
    </p>

    <ul
      v-if="passkeys.length"
      class="mt-4 divide-y divide-[var(--ogb-border)] rounded-md border border-[var(--ogb-border)]"
      data-testid="passkeys-list"
    >
      <li
        v-for="passkey in passkeys"
        :key="passkey.id"
        class="flex flex-wrap items-center gap-3 p-3"
        data-testid="passkey-row"
      >
        <UIcon
          name="i-lucide-key-round"
          class="size-4 shrink-0 text-[var(--ogb-accent)]"
        />

        <form
          v-if="editingId === passkey.id"
          class="flex min-w-0 flex-1 flex-wrap items-center gap-2"
          data-testid="passkey-rename-form"
          @submit.prevent="saveRename"
        >
          <UInput
            v-model="editingName"
            :maxlength="PASSKEY_NAME_MAX_LENGTH"
            :aria-label="t('settings.security.passkeys.nameLabel')"
            size="sm"
            class="min-w-0 flex-1"
            data-testid="passkey-rename-input"
            :autofocus="openedByUser"
            required
          />
          <UButton
            type="submit"
            size="sm"
            :disabled="!editingName.trim()"
          >
            {{ t('settings.security.passkeys.save') }}
          </UButton>
          <UButton
            size="sm"
            variant="ghost"
            @click="editingId = null"
          >
            {{ t('common.cancel') }}
          </UButton>
        </form>

        <template v-else>
          <div class="min-w-0 flex-1">
            <div class="flex flex-wrap items-center gap-2">
              <span
                class="truncate font-medium"
                data-testid="passkey-name"
              >{{ passkey.name }}</span>
              <UBadge
                v-if="passkey.isBackedUp"
                color="neutral"
                variant="subtle"
                size="sm"
                icon="i-lucide-cloud"
                :title="t('settings.security.passkeys.syncedHint')"
              >
                {{ t('settings.security.passkeys.synced') }}
              </UBadge>
            </div>
            <p class="mt-0.5 text-xs text-[var(--ogb-text-muted)]">
              {{ t('settings.security.passkeys.created') }} <RelativeTime :iso="passkey.createdAt" />
              ·
              <template v-if="passkey.lastUsedAt">
                {{ t('settings.security.passkeys.lastUsed') }} <RelativeTime :iso="passkey.lastUsedAt" />
              </template>
              <template v-else>
                {{ t('settings.security.passkeys.neverUsed') }}
              </template>
            </p>
          </div>
          <div class="flex gap-1">
            <UButton
              size="sm"
              variant="ghost"
              icon="i-lucide-pencil"
              :aria-label="t('settings.security.passkeys.rename')"
              :disabled="busy"
              data-testid="passkey-rename"
              @click="startRename(passkey)"
            />
            <UButton
              size="sm"
              variant="ghost"
              color="error"
              icon="i-lucide-trash-2"
              :aria-label="t('settings.security.passkeys.remove')"
              :disabled="busy"
              data-testid="passkey-remove"
              @click="emit('remove', passkey)"
            />
          </div>
        </template>
      </li>
    </ul>

    <p
      v-else
      class="mt-4 text-sm text-[var(--ogb-text-muted)]"
      data-testid="passkeys-empty"
    >
      {{ t('settings.security.passkeys.empty') }}
    </p>

    <UAlert
      v-if="error"
      class="mt-4"
      color="error"
      variant="subtle"
      :description="error"
    />

    <template #footer>
      <form
        v-if="adding"
        class="flex flex-wrap items-end gap-2"
        data-testid="passkey-add-form"
        @submit.prevent="submitAdd"
      >
        <UFormField
          :label="t('settings.security.passkeys.nameLabel')"
          class="w-full min-w-0 sm:w-auto sm:flex-1"
          required
        >
          <UInput
            v-model="newName"
            :maxlength="PASSKEY_NAME_MAX_LENGTH"
            :placeholder="t('settings.security.passkeys.namePlaceholder')"
            class="w-full"
            data-testid="passkey-name-input"
            :autofocus="openedByUser"
            required
          />
        </UFormField>
        <UButton
          type="submit"
          icon="i-lucide-fingerprint"
          :loading="busy"
          :disabled="!newName.trim()"
          data-testid="passkey-create"
        >
          {{ t('settings.security.passkeys.create') }}
        </UButton>
        <UButton
          variant="ghost"
          :disabled="busy"
          @click="adding = false"
        >
          {{ t('common.cancel') }}
        </UButton>
      </form>
      <UButton
        v-else
        icon="i-lucide-plus"
        :disabled="busy"
        data-testid="passkey-add"
        @click="openAdd"
      >
        {{ t('settings.security.passkeys.add') }}
      </UButton>
    </template>
  </UCard>
</template>
