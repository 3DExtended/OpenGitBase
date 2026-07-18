import { expect, test } from '@playwright/test'

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

test.describe('Pipeline visuals', () => {
  test.beforeEach(async ({ page }) => {
    await page.addInitScript(() => {
      document.cookie = 'ogb-site-gate-unlocked=1; Path=/; SameSite=Lax'
    })
  })

  test('gallery pipeline section', async ({ page }) => {
    await waitForApp(page)
    await expect(page.getByTestId('visual-pipelines')).toHaveScreenshot('visual-pipelines.png')
  })

  test('pipeline run detail page', async ({ page }) => {
    await page.goto('/demo-user/hello-world/pipelines/99999999-0000-0000-0000-000000000001?msw=1')
    await page.waitForLoadState('networkidle')
    await expect(page.getByRole('heading', { name: 'Pipelines' })).toBeVisible()
    await expect(page.locator('body')).toHaveScreenshot('pipeline-run-detail-page.png', { fullPage: true })
  })
})
