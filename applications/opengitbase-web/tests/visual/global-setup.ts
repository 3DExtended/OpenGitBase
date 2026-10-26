import { chromium, type BrowserContext, type FullConfig, type Locator, type Page } from '@playwright/test'

// `pnpm dev` passes the webServer readiness probe long before the client app is
// compiled: Vite transforms each module on the first browser request for it, so
// the first spec to run against a fresh server needs longer than the 5 s expect
// timeout to render. Load the app here so every spec starts against a warm
// server, whichever one runs first. The bound is generous because it is paid
// once per run, not per assertion.
const WARM_UP_TIMEOUT_MS = 5 * 60_000

export default async function globalSetup(config: FullConfig): Promise<void> {
  const baseURL = config.projects[0]?.use.baseURL
  if (!baseURL) {
    throw new Error('Visual warm-up needs a baseURL in playwright.config.ts')
  }

  const startedAt = Date.now()
  const browser = await chromium.launch()
  try {
    const context = await browser.newContext({ baseURL })
    context.setDefaultTimeout(WARM_UP_TIMEOUT_MS)

    // A dependency missing from `vite.optimizeDeps.include` in nuxt.config.ts is
    // discovered at runtime the first time a pass imports it; re-optimizing
    // reloads the page and invalidates every module transformed so far. A
    // second pass can discover nothing new, so the server stays warm after it.
    for (let pass = 1; pass <= 2; pass++) {
      await warm(context)
    }
  }
  finally {
    await browser.close()
  }
  console.log(`Visual warm-up: dev server ready in ${((Date.now() - startedAt) / 1000).toFixed(1)}s`)
}

async function warm(context: BrowserContext): Promise<void> {
  // The gallery renders most components and starts the MSW worker most specs use.
  await render(context, '/__visual__/?msw=1', page => page.getByTestId('visual-gallery'))
  await render(context, '/__visual__/admin-ci', page => page.getByTestId('visual-admin-ci'))
  // The default layout, which neither gallery route uses, then every other page.
  await render(context, '/?msw=1', page => page.locator('.ogb-shell'), loadEveryPageChunk)
}

async function render(
  context: BrowserContext,
  path: string,
  ready: (page: Page) => Locator,
  andThen?: (page: Page) => Promise<void>,
): Promise<void> {
  // A page per step, so a Vite reload still pending on the previous step's page
  // cannot interrupt this navigation.
  const page = await context.newPage()
  try {
    await page.goto(path, { waitUntil: 'commit' })
    // Locator waits survive a Vite reload, so this resolves on the settled page.
    await ready(page).waitFor({ state: 'visible' })
    await page.waitForLoadState('networkidle')
    await andThen?.(page)
  }
  finally {
    await page.close()
  }
}

// Import every route's page component (sign-in, pitch, merge requests, ...) so
// specs that open an app route first find its module graph already transformed.
async function loadEveryPageChunk(page: Page): Promise<void> {
  for (let attempt = 1; ; attempt++) {
    try {
      await page.evaluate(async () => {
        type RouteRecord = { components?: Record<string, unknown> | null }
        const { $router } = (window as unknown as {
          useNuxtApp: () => { $router: { getRoutes: () => RouteRecord[] } }
        }).useNuxtApp()
        await Promise.all($router.getRoutes().map((route) => {
          const component = route.components?.default
          return typeof component === 'function' ? component() : undefined
        }))
      })
      return
    }
    catch (error) {
      // A dependency first seen in one of these chunks makes Vite reload the page
      // mid-import; wait for the reloaded app and import the rest.
      if (attempt === 5 || !String(error).includes('Execution context was destroyed')) {
        throw error
      }
      await page.locator('.ogb-shell').waitFor({ state: 'visible' })
      await page.waitForLoadState('networkidle')
    }
  }
}
