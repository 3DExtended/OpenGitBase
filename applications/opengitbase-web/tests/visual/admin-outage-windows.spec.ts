import { expect, test } from '@playwright/test'
import { waitForPageReady } from './helpers'

async function waitForApp(page: import('@playwright/test').Page) {
  await page.goto('/__visual__/?msw=1')
  await waitForPageReady(page)
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

test.describe('Admin outage window visuals', () => {
  test.beforeEach(async ({ page }) => {
    await page.addInitScript(() => {
      document.cookie = 'ogb-site-gate-unlocked=1; Path=/; SameSite=Lax'
    })
  })

  test('gallery admin outage windows section', async ({ page }) => {
    await waitForApp(page)
    await expect(page.getByTestId('visual-admin-outage-windows')).toHaveScreenshot('visual-admin-outage-windows.png')
  })
})
