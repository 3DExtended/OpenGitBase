import { test, expect } from '@playwright/test'

async function waitForApp(page: import('@playwright/test').Page) {
  await page.goto('/__visual__/?msw=1')
  await page.waitForLoadState('networkidle')
  await page.evaluate(async () => {
    await document.fonts.ready
  })
  await page.addStyleTag({
    content: `
      *, *::before, *::after {
        animation-duration: 0s !important;
        animation-delay: 0s !important;
        transition-duration: 0s !important;
        transition-delay: 0s !important;
      }
    `,
  })
}

test.beforeEach(async ({ page }) => {
  await page.addInitScript(() => {
    document.cookie = 'ogb-site-gate-unlocked=1; Path=/; SameSite=Lax'
    void navigator.serviceWorker.getRegistrations().then(registrations =>
      Promise.all(registrations.map(registration => registration.unregister())),
    )
  })
})

test.describe('Behavioral UI @regression', () => {
  test('sign-in form renders required fields', async ({ page }) => {
    await page.goto('/sign-in?msw=1')
    await page.waitForLoadState('networkidle')
    await expect(page.getByRole('heading', { name: /sign in/i })).toBeVisible()
    await expect(page.locator('input[type="password"]')).toBeVisible()
  })

  test('sign-up form renders', async ({ page }) => {
    await page.goto('/sign-up?msw=1')
    await page.waitForLoadState('networkidle')
    await expect(page.getByRole('heading', { name: /sign up|create account/i })).toBeVisible()
  })

  test('visual gallery exposes header and sidebar fixtures', async ({ page }) => {
    await waitForApp(page)
    await expect(page.getByTestId('visual-gallery')).toBeVisible()
    await expect(page.getByTestId('visual-header')).toBeVisible()
    await expect(page.getByTestId('visual-sidebar')).toBeVisible()
  })

  test('merge request overview fixture renders list region', async ({ page }) => {
    await waitForApp(page)
    await expect(page.getByTestId('visual-merge-requests-overview')).toBeVisible()
  })

  test('discussion sub-thread fixture renders reply control', async ({ page }) => {
    await waitForApp(page)
    await expect(page.getByTestId('visual-discussion-sub-threads')).toBeVisible()
    await expect(page.getByTestId('discussion-sub-thread-reply').first()).toBeVisible()
  })

  test('branch settings fixture renders protected rules section', async ({ page }) => {
    await waitForApp(page)
    await expect(page.getByTestId('visual-branches-settings')).toBeVisible()
  })

  test('auth card fixture shows verification banner slot', async ({ page }) => {
    await waitForApp(page)
    await expect(page.getByTestId('visual-auth-card')).toBeVisible()
    await expect(page.getByTestId('visual-verification-banner')).toBeVisible()
  })

  test('forgot-password route is reachable', async ({ page }) => {
    await page.goto('/forgot-password?msw=1')
    await page.waitForLoadState('networkidle')
    await expect(page.locator('body')).toContainText(/password/i)
  })

  test('explore page loads under MSW', async ({ page }) => {
    await page.goto('/explore?msw=1')
    await page.waitForLoadState('networkidle')
    await expect(page.locator('body')).toBeVisible()
  })

  test('profile settings route loads', async ({ page }) => {
    await page.goto('/settings/profile?msw=1')
    await page.waitForLoadState('networkidle')
    await expect(page.locator('body')).toBeVisible()
  })

  test('storage meter fixtures render warning state', async ({ page }) => {
    await waitForApp(page)
    await expect(page.getByTestId('visual-storage-meter-warning')).toBeVisible()
  })
})
