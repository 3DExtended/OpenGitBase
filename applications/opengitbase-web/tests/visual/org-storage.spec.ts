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

test.describe('Org storage visuals', () => {
  test.beforeEach(async ({ page }) => {
    await page.addInitScript(() => {
      localStorage.setItem('ogb-site-gate-unlocked', '1')
    })
  })

  test('gallery org storage empty state', async ({ page }) => {
    await waitForApp(page)
    await expect(page.getByTestId('visual-org-storage-empty')).toHaveScreenshot('visual-org-storage-empty.png')
  })

  test('gallery org storage enrollment success', async ({ page }) => {
    await waitForApp(page)
    await expect(page.getByTestId('visual-org-storage-enrollment')).toHaveScreenshot('visual-org-storage-enrollment.png')
  })

  test('gallery org storage node edit open', async ({ page }) => {
    await waitForApp(page)
    await expect(page.getByTestId('visual-org-storage-edit')).toHaveScreenshot('visual-org-storage-edit.png')
  })

  test('gallery org storage unhealthy node', async ({ page }) => {
    await waitForApp(page)
    await expect(page.getByTestId('visual-org-storage-unhealthy')).toHaveScreenshot('visual-org-storage-unhealthy.png')
  })
})
