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

test.describe('Status outage timeline visuals', () => {
  test.beforeEach(async ({ page }) => {
    await page.addInitScript(() => {
      document.cookie = 'ogb-site-gate-unlocked=1; Path=/; SameSite=Lax'
    })
  })

  test('gallery outage timeline section', async ({ page }) => {
    await waitForApp(page)
    await expect(page.getByTestId('visual-status-outage-timeline')).toHaveScreenshot('visual-status-outage-timeline.png')
  })

  test('gallery outage timeline empty state', async ({ page }) => {
    await waitForApp(page)
    await expect(page.getByTestId('visual-status-outage-timeline-empty')).toHaveScreenshot('visual-status-outage-timeline-empty.png')
  })
})
