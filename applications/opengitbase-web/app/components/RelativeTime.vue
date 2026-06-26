<script setup lang="ts">
import { useMediaQuery } from '@vueuse/core'
import { formatAbsoluteTime, formatRelativeTime } from '~/utils/relativeTime'

const props = defineProps<{
  iso: string
  class?: string
}>()

const { locale } = useI18n()
const canHover = useMediaQuery('(hover: hover) and (pointer: fine)')
const showAbsolute = ref(false)

const relative = computed(() => formatRelativeTime(props.iso, { locale: locale.value }))
const absolute = computed(() => formatAbsoluteTime(props.iso, { locale: locale.value }))
const display = computed(() =>
  !canHover.value && showAbsolute.value ? absolute.value : relative.value,
)

function toggleAbsolute(): void {
  if (!canHover.value) {
    showAbsolute.value = !showAbsolute.value
  }
}
</script>

<template>
  <time
    :datetime="iso"
    :title="absolute"
    :class="[props.class, canHover ? 'cursor-help' : 'cursor-pointer']"
    :aria-expanded="canHover ? undefined : showAbsolute"
    @click="toggleAbsolute"
  >
    {{ display }}
  </time>
</template>
