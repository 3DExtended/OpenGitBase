import { expect, test } from './helpers'

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

test.describe('Admin Git LFS visuals', () => {
  test.beforeEach(async ({ page }) => {
    await page.addInitScript(() => {
      document.cookie = 'ogb-site-gate-unlocked=1; Path=/; SameSite=Lax'
    })
  })

  test('gallery admin LFS copies with a restore in progress', async ({ page }) => {
    await waitForApp(page)
    const section = page.getByTestId('visual-admin-lfs-copy-status')
    await expect(section.getByTestId('admin-lfs-restore')).toContainText('Restoring LFS objects to storage-4: 110 of 240')
    await expect(section.getByTestId('admin-lfs-missing')).toContainText('131 missing copies')
    await expect(section).toHaveScreenshot('visual-admin-lfs-copy-status.png')
  })

  test('gallery admin LFS fleet totals', async ({ page }) => {
    await waitForApp(page)
    const section = page.getByTestId('visual-admin-lfs-fleet-summary')
    await expect(section).toContainText('131 missing copies in 2 repositories')
    await expect(section).toHaveScreenshot('visual-admin-lfs-fleet-summary.png')
  })
})
