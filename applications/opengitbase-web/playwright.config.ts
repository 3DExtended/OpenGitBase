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
  globalSetup: './tests/visual/global-setup.ts',
  // networkidle and action waits are bounded only by the test timeout. Under heavy
  // machine load a warm page alone can take 20-30 s to load from the dev server;
  // toHaveScreenshot keeps the 5 s expect default.
  timeout: 60_000,
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 1,
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
    // nuxt dev needs well over the 60 s default to answer when the machine is busy.
    timeout: 180_000,
    reuseExistingServer: false,
    env: {
      NUXT_PUBLIC_MSW: 'false',
      NUXT_PUBLIC_SITE_GATE_ENABLED: 'false',
      NUXT_PUBLIC_DEPLOY_SHA: 'visual-test',
    },
  },
})
