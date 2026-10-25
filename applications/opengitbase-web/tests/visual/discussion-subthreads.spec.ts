import { test, expect } from '@playwright/test'
import { freezeClock, waitForPageReady } from './helpers'

async function waitForApp(page: import('@playwright/test').Page) {
  await page.addInitScript(() => {
    document.cookie = 'ogb-site-gate-unlocked=1; Path=/; SameSite=Lax'
  })
  await freezeClock(page)
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

test.describe('Discussion sub-threads', () => {
  test('open thread with reply', async ({ page }) => {
    await waitForApp(page)
    await expect(page.getByTestId('visual-discussion-sub-threads')).toHaveScreenshot(
      'discussion-sub-threads.png',
      { fullPage: false },
    )
  })
})
