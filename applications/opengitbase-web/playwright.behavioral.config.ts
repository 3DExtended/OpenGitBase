import { defineConfig, devices } from '@playwright/test'

const playwrightPort = Number(process.env.PLAYWRIGHT_PORT ?? 3100)

export default defineConfig({
  testDir: './tests/behavioral',
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: [['list']],
  use: {
    baseURL: process.env.PLAYWRIGHT_BASE_URL ?? `http://localhost:${playwrightPort}`,
    trace: 'on-first-retry',
    colorScheme: 'light',
    ...devices['Desktop Chrome'],
    viewport: { width: 1280, height: 720 },
  },
  webServer: process.env.PLAYWRIGHT_BASE_URL
    ? undefined
    : {
        command: `pnpm dev --port ${playwrightPort}`,
        url: `http://localhost:${playwrightPort}`,
        reuseExistingServer: false,
        env: {
          NUXT_PUBLIC_MSW: 'false',
          NUXT_PUBLIC_SITE_GATE_ENABLED: 'false',
        },
      },
})
