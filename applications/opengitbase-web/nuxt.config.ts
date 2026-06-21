export default defineNuxtConfig({
  ssr: false,

  modules: [
    '@nuxt/ui',
    '@pinia/nuxt',
    '@vueuse/nuxt',
    '@nuxtjs/i18n',
    '@nuxt/eslint',
  ],

  css: ['~/assets/main.css'],

  devtools: { enabled: process.env.NUXT_PUBLIC_MSW !== 'true' },

  runtimeConfig: {
    public: {
      instanceName: process.env.NUXT_PUBLIC_INSTANCE_NAME ?? 'OpenGitBase',
      instanceLogoUrl: process.env.NUXT_PUBLIC_INSTANCE_LOGO_URL ?? '',
      apiBase: process.env.NUXT_PUBLIC_API_BASE ?? '/api',
      msw: process.env.NUXT_PUBLIC_MSW ?? 'false',
      siteGateEnabled: process.env.NUXT_PUBLIC_SITE_GATE_ENABLED !== 'false',
    },
  },

  colorMode: {
    preference: 'light',
    fallback: 'light',
  },

  i18n: {
    locales: [
      {
        code: 'en',
        language: 'en-US',
        file: 'en.json',
      },
    ],
    defaultLocale: 'en',
    langDir: 'locales',
    strategy: 'no_prefix',
    detectBrowserLanguage: false,
    bundle: {
      optimizeTranslationDirective: false,
    },
  },

  vite: {
    server: {
      proxy: {
        '/api': {
          target: process.env.NUXT_DEV_API_PROXY ?? 'http://localhost:5000',
          changeOrigin: true,
        },
      },
    },
  },

  compatibilityDate: '2025-06-21',
})
