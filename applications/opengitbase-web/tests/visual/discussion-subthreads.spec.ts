import { test, expect } from '@playwright/test'

const baseURL = process.env.PLAYWRIGHT_BASE_URL ?? 'http://localhost:3000'

async function waitForApp(page: import('@playwright/test').Page) {
  await page.addInitScript(() => {
    localStorage.setItem('ogb-site-gate-unlocked', '1')
  })
  await page.goto(`${baseURL}/__visual__/?msw=1`)
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

test.describe('Discussion sub-threads', () => {
  test('open thread with reply', async ({ page }) => {
    await waitForApp(page)
    await expect(page.getByTestId('visual-discussion-sub-threads')).toHaveScreenshot(
      'discussion-sub-threads.png',
      { fullPage: false },
    )
  })
})
