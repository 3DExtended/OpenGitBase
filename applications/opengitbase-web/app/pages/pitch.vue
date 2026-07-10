<script setup lang="ts">
import Reveal from 'reveal.js'
import { communityPitchSlides } from '~/data/communityPitchSlides'
import '~/assets/pitch.css'

definePageMeta({
  layout: false,
})

const { instanceName } = useInstanceBranding()
const { t } = useI18n()

const revealRoot = ref<HTMLElement | null>(null)
let deck: InstanceType<typeof Reveal> | null = null

useHead({
  title: () => t('pitch.pageTitle'),
})

onMounted(async () => {
  if (!revealRoot.value) {
    return
  }

  deck = new Reveal(revealRoot.value, {
    hash: true,
    slideNumber: 'c/t',
    width: 1280,
    height: 720,
    margin: 0.08,
    transition: 'slide',
    backgroundTransition: 'fade',
    controls: true,
    progress: true,
    center: false,
  })

  await deck.initialize()
})

onBeforeUnmount(() => {
  deck?.destroy()
  deck = null
})

function linkClass(primary?: boolean): string {
  return primary ? 'pitch-link pitch-link-primary' : 'pitch-link'
}
</script>

<template>
  <div
    class="pitch-shell"
    data-testid="community-pitch"
  >
    <header class="pitch-toolbar">
      <NuxtLink
        to="/"
        class="pitch-toolbar-brand"
      >
        <UIcon
          name="i-lucide-git-branch"
          class="size-5 text-[var(--ogb-accent)]"
        />
        <span>{{ instanceName }}</span>
      </NuxtLink>
      <span class="pitch-toolbar-hint hidden sm:inline">
        {{ t('pitch.toolbarHint') }}
      </span>
      <UButton
        to="/"
        color="neutral"
        variant="ghost"
        size="sm"
        icon="i-lucide-x"
      >
        {{ t('pitch.exit') }}
      </UButton>
    </header>

    <div class="pitch-deck-wrap">
      <div
        ref="revealRoot"
        class="reveal"
        data-testid="community-pitch-deck"
      >
        <div class="slides">
          <section
            v-for="slide in communityPitchSlides"
            :id="slide.id"
            :key="slide.id"
            :data-id="slide.id"
          >
            <div
              v-if="slide.layout === 'title'"
              class="pitch-slide-title"
            >
              <h1>{{ slide.title }}</h1>
              <p v-if="slide.subtitle">
                {{ slide.subtitle }}
              </p>
            </div>

            <div
              v-else-if="slide.layout === 'columns'"
              class="pitch-slide-columns"
            >
              <h2>{{ slide.title }}</h2>
              <p
                v-if="slide.subtitle"
                class="pitch-slide-subtitle"
              >
                {{ slide.subtitle }}
              </p>
              <div class="pitch-columns">
                <div
                  v-for="column in slide.columns"
                  :key="column.heading"
                  class="pitch-column"
                >
                  <h3>{{ column.heading }}</h3>
                  <ul>
                    <li
                      v-for="item in column.items"
                      :key="item"
                    >
                      {{ item }}
                    </li>
                  </ul>
                </div>
              </div>
              <p
                v-if="slide.note"
                class="pitch-note"
              >
                {{ slide.note }}
              </p>
              <div
                v-if="slide.links?.length"
                class="pitch-links"
              >
                <a
                  v-for="link in slide.links"
                  :key="link.href"
                  :href="link.href"
                  :class="linkClass(link.primary)"
                  target="_blank"
                  rel="noopener noreferrer"
                >
                  {{ link.label }}
                </a>
              </div>
            </div>

            <div
              v-else-if="slide.layout === 'cta'"
              class="pitch-slide-cta"
            >
              <h2>{{ slide.title }}</h2>
              <p
                v-if="slide.lead"
                class="pitch-slide-lead"
              >
                {{ slide.lead }}
              </p>
              <div
                v-if="slide.links?.length"
                class="pitch-links"
              >
                <a
                  v-for="link in slide.links"
                  :key="link.href"
                  :href="link.href"
                  :class="linkClass(link.primary)"
                  target="_blank"
                  rel="noopener noreferrer"
                >
                  {{ link.label }}
                </a>
              </div>
              <p
                v-if="slide.note"
                class="pitch-note"
              >
                {{ slide.note }}
              </p>
            </div>

            <div
              v-else
              class="pitch-slide-default"
            >
              <h2>{{ slide.title }}</h2>
              <p
                v-if="slide.subtitle"
                class="pitch-slide-subtitle"
              >
                {{ slide.subtitle }}
              </p>
              <p
                v-if="slide.lead"
                class="pitch-slide-lead"
              >
                {{ slide.lead }}
              </p>
              <ul v-if="slide.bullets?.length">
                <li
                  v-for="bullet in slide.bullets"
                  :key="bullet"
                >
                  {{ bullet }}
                </li>
              </ul>
              <p
                v-if="slide.note"
                class="pitch-note"
              >
                {{ slide.note }}
              </p>
              <div
                v-if="slide.links?.length"
                class="pitch-links"
              >
                <a
                  v-for="link in slide.links"
                  :key="link.href"
                  :href="link.href"
                  :class="linkClass(link.primary)"
                  target="_blank"
                  rel="noopener noreferrer"
                >
                  {{ link.label }}
                </a>
              </div>
            </div>
          </section>
        </div>
      </div>
    </div>
  </div>
</template>
