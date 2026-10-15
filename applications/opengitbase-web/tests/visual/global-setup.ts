import { chromium, type FullConfig } from '@playwright/test'

type RouterLike = {
  getRoutes(): Array<{ components?: Record<string, unknown> | null }>
}

type AppContainer = HTMLElement & {
  __vue_app__?: { config: { globalProperties: { $router?: RouterLike } } }
}

// A cold first visit compiles the app shell and that page's module graph, which
// can take minutes under heavy machine load.
const WARM_UP_TIMEOUT = 300_000

// The dev server compiles modules on demand, so whichever test first opens a page
// pays for compiling it and times out its readiness wait under heavy machine load.
// Open the gallery once (app shell, MSW mocks, most components) and import every
// route's page module through the Nuxt router, so the suite starts against a warm
// server without keeping a list of routes in sync with the specs.
export default async function warmUpDevServer(config: FullConfig): Promise<void> {
  const baseURL = config.projects[0]?.use.baseURL
  if (!baseURL) {
    return
  }

  const startedAt = Date.now()
  const browser = await chromium.launch()
  try {
    const page = await browser.newPage({ baseURL })
    page.setDefaultTimeout(WARM_UP_TIMEOUT)
    await page.goto('/__visual__/?msw=1')
    await page.waitForFunction(() => !!document.querySelector<AppContainer>('#__nuxt')?.__vue_app__)
    const routeCount = await page.evaluate(async () => {
      const router = document.querySelector<AppContainer>('#__nuxt')?.__vue_app__?.config.globalProperties.$router
      const loaders = (router?.getRoutes() ?? [])
        .flatMap(route => Object.values(route.components ?? {}))
        .filter((component): component is () => Promise<unknown> => typeof component === 'function')
      await Promise.all(loaders.map(load => load()))
      return loaders.length
    })
    await page.waitForLoadState('networkidle')
    console.log(`[visual warm-up] gallery and ${routeCount} route modules loaded in ${Math.round((Date.now() - startedAt) / 1000)}s`)
  }
  catch (error) {
    // Best effort: the specs' own readiness waits still decide pass or fail.
    console.warn(`[visual warm-up] skipped: ${error instanceof Error ? error.message : String(error)}`)
  }
  finally {
    await browser.close()
  }
}
