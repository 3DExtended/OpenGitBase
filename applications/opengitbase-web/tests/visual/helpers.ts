import type { Page } from '@playwright/test'

// RelativeTime renders the fixed timestamps in the MSW and gallery fixtures as
// relative text ("Updated 3 months ago"), which rolls over as the calendar
// advances. Pin the browser clock before navigation so those snapshots render
// the same text on any day.
export async function freezeClock(page: Page): Promise<void> {
  await page.clock.setFixedTime(new Date('2026-09-01T12:00:00Z'))
}

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
