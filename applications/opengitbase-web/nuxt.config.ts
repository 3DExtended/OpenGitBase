import { execSync } from 'node:child_process'
import { copyFileSync, existsSync, readFileSync, unlinkSync } from 'node:fs'
import { dirname, join } from 'node:path'
import { fileURLToPath } from 'node:url'

const webRoot = dirname(fileURLToPath(import.meta.url))
const mockWorkerPath = join(webRoot, 'public/mockServiceWorker.js')
const mockWorkerBackupPath = join(webRoot, 'public/.mockServiceWorker.js.build-backup')

const repoRoot = join(dirname(fileURLToPath(import.meta.url)), '../..')

function readDevDeployVersion(): string {
  if (process.env.NUXT_PUBLIC_DEPLOY_VERSION) {
    return process.env.NUXT_PUBLIC_DEPLOY_VERSION
  }
  try {
    const majorMinor = readFileSync(join(repoRoot, 'VERSION'), 'utf8').trim()
    return `v${majorMinor}.0`
  } catch {
    return ''
  }
}

function readDevDeploySha(): string {
  if (process.env.NUXT_PUBLIC_DEPLOY_SHA) {
    return process.env.NUXT_PUBLIC_DEPLOY_SHA
  }
  try {
    return execSync('git rev-parse --short HEAD', { cwd: repoRoot, encoding: 'utf8' }).trim()
  } catch {
    return ''
  }
}

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
      deployVersion: process.env.NUXT_PUBLIC_DEPLOY_VERSION ?? readDevDeployVersion(),
      deploySha: process.env.NUXT_PUBLIC_DEPLOY_SHA ?? readDevDeploySha(),
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

  hooks: {
    'build:before'() {
      if (process.env.NUXT_PUBLIC_MSW === 'true') {
        return
      }
      if (existsSync(mockWorkerPath)) {
        copyFileSync(mockWorkerPath, mockWorkerBackupPath)
        unlinkSync(mockWorkerPath)
      }
    },
    'build:done'() {
      if (process.env.NUXT_PUBLIC_MSW === 'true') {
        return
      }
      if (existsSync(mockWorkerBackupPath)) {
        copyFileSync(mockWorkerBackupPath, mockWorkerPath)
        unlinkSync(mockWorkerBackupPath)
      }
    },
  },
})
