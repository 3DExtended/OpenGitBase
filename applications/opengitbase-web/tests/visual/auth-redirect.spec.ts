import { expect, test } from '@playwright/test'

test.describe('Auth redirect safety', () => {
  test.beforeEach(async ({ page }) => {
    await page.addInitScript(() => {
      localStorage.setItem('ogb-site-gate-unlocked', '1')
      void navigator.serviceWorker.getRegistrations().then(registrations =>
        Promise.all(registrations.map(registration => registration.unregister())),
      )
    })
  })

  async function signIn(page: import('@playwright/test').Page) {
    await page.goto('/sign-in?msw=1')
    await page.getByLabel(/username/i).fill('demo-user')
    await page.getByLabel(/^password$/i).fill('password')
    await page.getByRole('button', { name: /sign in/i }).click()
  }

  test('sign-in honors safe relative redirect', async ({ page }) => {
    await page.goto('/sign-in?redirect=/settings/profile&msw=1')
    await page.getByLabel(/username/i).fill('demo-user')
    await page.getByLabel(/^password$/i).fill('password')
    await page.getByRole('button', { name: /sign in/i }).click()
    await expect(page).toHaveURL(/\/settings\/profile/)
  })

  test('sign-in blocks protocol-relative redirect', async ({ page }) => {
    await page.goto('/sign-in?redirect=%2F%2Fevil.com&msw=1')
    await signIn(page)
    await expect(page).toHaveURL(/\/$/)
  })

  test('sign-in blocks absolute redirect', async ({ page }) => {
    await page.goto('/sign-in?redirect=https%3A%2F%2Fevil.com&msw=1')
    await signIn(page)
    await expect(page).toHaveURL(/\/$/)
  })
})
