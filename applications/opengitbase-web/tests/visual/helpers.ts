import { expect, test as base, type Page } from '@playwright/test'

export { expect }

// Budget for the wait that decides a page has rendered after navigation. The dev
// server serves hundreds of unbundled modules per page, which outlasts the 5 s
// default expect timeout under heavy machine load. Pass it only to readiness
// assertions: toHaveScreenshot keeps the default so a snapshot that never settles
// still fails fast.
export const PAGE_READY_TIMEOUT = 30_000

// networkidle alone is not a readiness signal when the machine is starved for CPU:
// the network goes quiet while the browser is still evaluating modules, so it can
// fire before the app has mounted and leave the render and web-font swap to
// toHaveScreenshot. The SPA shell's #__nuxt stays empty until Vue mounts, so also
// wait for its first child and then for the fonts it laid out.
export async function waitForPageReady(page: Page): Promise<void> {
  await page.waitForLoadState('networkidle')
  await expect(page.locator('#__nuxt > *').first()).toBeVisible({ timeout: PAGE_READY_TIMEOUT })
  await page.evaluate(async () => {
    await document.fonts.ready
  })
}

export const PINNED_NOW = '2026-09-01T12:00:00.000Z'

// RelativeTime renders the fixed timestamps in the MSW and route fixtures as
// relative text ("Updated 3 months ago"), which rolls over as the calendar
// advances. Every visual spec takes `test` from here, so the browser clock is
// pinned before any hook or test navigates and snapshots render the same text
// on any day.
export const test = base.extend<{ pinnedClock: void }>({
  pinnedClock: [async ({ page }, use) => {
    await page.clock.setFixedTime(PINNED_NOW)
    await use()
  }, { auto: true }],
})

/** Prevent MSW service worker from intercepting Playwright page.route mocks. */
export async function blockServiceWorker(page: Page): Promise<void> {
  await page.addInitScript(() => {
    document.cookie = 'ogb-site-gate-unlocked=1; Path=/; SameSite=Lax'
    void navigator.serviceWorker.getRegistrations().then(registrations =>
      Promise.all(registrations.map(registration => registration.unregister())),
    )
    navigator.serviceWorker.register = async () => ({
      installing: null,
      waiting: null,
      active: null,
      scope: '/',
      updatefound: null,
      onupdatefound: null,
      addEventListener: () => {},
      removeEventListener: () => {},
      dispatchEvent: () => true,
      unregister: async () => true,
      update: async () => {},
    }) as ServiceWorkerRegistration
  })
}
