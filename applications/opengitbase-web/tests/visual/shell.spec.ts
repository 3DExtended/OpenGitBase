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

test.describe('Shell components', () => {
  test('visual gallery', async ({ page }) => {
    await waitForApp(page)
    await expect(page.getByTestId('visual-gallery')).toHaveScreenshot('visual-gallery.png', {
      fullPage: true,
    })
  })

  test('app header', async ({ page }) => {
    await waitForApp(page)
    await expect(page.getByTestId('visual-header')).toHaveScreenshot('app-header.png')
  })

  test('app sidebar', async ({ page }) => {
    await waitForApp(page)
    await expect(page.getByTestId('visual-sidebar')).toHaveScreenshot('app-sidebar.png')
  })
})

test.describe('Auth screens', () => {
  test('sign-in', async ({ page }) => {
    await page.goto('/sign-in?msw=1')
    await page.waitForLoadState('networkidle')
    await page.evaluate(async () => { await document.fonts.ready })
    await expect(page.locator('body')).toHaveScreenshot('sign-in.png', {
      fullPage: true,
    })
  })

  test('sign-up', async ({ page }) => {
    await page.goto('/sign-up?msw=1')
    await page.waitForLoadState('networkidle')
    await page.evaluate(async () => { await document.fonts.ready })
    await expect(page.locator('body')).toHaveScreenshot('sign-up.png', {
      fullPage: true,
    })
  })

  test('forgot-password', async ({ page }) => {
    await page.goto('/forgot-password?msw=1')
    await page.waitForLoadState('networkidle')
    await expect(page.locator('body')).toHaveScreenshot('forgot-password.png', {
      fullPage: true,
    })
  })

  test('reset-password', async ({ page }) => {
    await page.goto('/reset-password?msw=1')
    await page.waitForLoadState('networkidle')
    await expect(page.locator('body')).toHaveScreenshot('reset-password.png', {
      fullPage: true,
    })
  })

  test('verify-email', async ({ page }) => {
    await page.goto('/verify-email?msw=1')
    await page.waitForLoadState('networkidle')
    await expect(page.locator('body')).toHaveScreenshot('verify-email.png', {
      fullPage: true,
    })
  })
})

test.describe('Settings screens', () => {
  test('settings', async ({ page }) => {
    await page.goto('/settings?msw=1')
    await page.waitForLoadState('networkidle')
    await expect(page.locator('body')).toHaveScreenshot('settings.png', {
      fullPage: true,
    })
  })

  test('ssh-keys', async ({ page }) => {
    await page.goto('/settings/ssh-keys?msw=1')
    await page.waitForLoadState('networkidle')
    await expect(page.locator('body')).toHaveScreenshot('ssh-keys.png', {
      fullPage: true,
    })
  })
})

test.describe('Discovery screens', () => {
  test('explore', async ({ page }) => {
    await page.goto('/explore?msw=1')
    await page.waitForLoadState('networkidle')
    await expect(page.locator('body')).toHaveScreenshot('explore.png', {
      fullPage: true,
    })
  })

  test('logged-out home', async ({ page }) => {
    await page.route('**/api/account/me', route => route.fulfill({ status: 401, body: '' }))
    await page.goto('/?msw=1')
    await page.waitForLoadState('networkidle')
    await expect(page.locator('body')).toHaveScreenshot('home-logged-out.png', {
      fullPage: true,
    })
  })

  test('profile', async ({ page }) => {
    await page.goto('/demo-user?msw=1')
    await page.waitForLoadState('networkidle')
    await expect(page.locator('body')).toHaveScreenshot('profile.png', {
      fullPage: true,
    })
  })
})

test.describe('Repository screens', () => {
  test('dashboard', async ({ page }) => {
    await page.goto('/?msw=1')
    await page.waitForLoadState('networkidle')
    await expect(page.locator('body')).toHaveScreenshot('dashboard.png', {
      fullPage: true,
    })
  })

  test('repo overview', async ({ page }) => {
    await page.goto('/demo-user/hello-world?msw=1')
    await page.waitForLoadState('networkidle')
    await expect(page.locator('body')).toHaveScreenshot('repo-overview.png', {
      fullPage: true,
    })
  })

  test('repo settings', async ({ page }) => {
    await page.goto('/demo-user/hello-world/settings?msw=1')
    await page.waitForLoadState('networkidle')
    await expect(page.locator('body')).toHaveScreenshot('repo-settings.png', {
      fullPage: true,
    })
  })

  test('repo members', async ({ page }) => {
    await page.goto('/demo-user/hello-world/members?msw=1')
    await page.waitForLoadState('networkidle')
    await expect(page.locator('body')).toHaveScreenshot('repo-members.png', {
      fullPage: true,
    })
  })

  test('new repo', async ({ page }) => {
    await page.goto('/repos/new?msw=1')
    await page.waitForLoadState('networkidle')
    await expect(page.locator('body')).toHaveScreenshot('repo-new.png', {
      fullPage: true,
    })
  })

  test('new org', async ({ page }) => {
    await page.goto('/orgs/new?msw=1')
    await page.waitForLoadState('networkidle')
    await expect(page.locator('body')).toHaveScreenshot('org-new.png', {
      fullPage: true,
    })
  })
})

test.describe('Storage meter', () => {
  test('storage warning state', async ({ page }) => {
    await waitForApp(page)
    await expect(page.getByTestId('visual-storage-meter-warning')).toHaveScreenshot('storage-warning.png')
  })
})
