import { test as base, type Page } from '@playwright/test'

export { expect } from '@playwright/test'

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
