import type { Page } from '@playwright/test'

/** Prevent MSW service worker from intercepting Playwright page.route mocks. */
export async function blockServiceWorker(page: Page): Promise<void> {
  await page.addInitScript(() => {
    localStorage.setItem('ogb-site-gate-unlocked', '1')
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
