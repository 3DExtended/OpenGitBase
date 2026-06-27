import { defineConfig, devices } from '@playwright/test'

const viewports = [
  { name: 'mobile', width: 375, height: 812 },
  { name: 'tablet', width: 768, height: 1024 },
  { name: 'desktop', width: 1280, height: 720 },
] as const

const playwrightPort = Number(process.env.PLAYWRIGHT_PORT ?? 3100)
const playwrightBaseUrl = process.env.PLAYWRIGHT_BASE_URL ?? `http://localhost:${playwrightPort}`

export default defineConfig({
  testDir: './tests/visual',
  fullyParallel: true,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 1,
  workers: process.env.CI ? 1 : undefined,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    baseURL: playwrightBaseUrl,
    trace: 'on-first-retry',
    colorScheme: 'light',
  },
  expect: {
    toHaveScreenshot: {
      maxDiffPixels: 0,
    },
  },
  projects: viewports.map(({ name, width, height }) => ({
    name,
    use: {
      ...devices['Desktop Chrome'],
      viewport: { width, height },
    },
  })),
  webServer: {
    command: `pnpm dev --port ${playwrightPort}`,
    url: playwrightBaseUrl,
    reuseExistingServer: !process.env.CI,
    env: {
      NUXT_PUBLIC_MSW: 'true',
      NUXT_PUBLIC_SITE_GATE_ENABLED: 'false',
    },
  },
})
