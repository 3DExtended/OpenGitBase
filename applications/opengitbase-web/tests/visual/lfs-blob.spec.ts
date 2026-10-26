import { expect, test, waitForPageReady } from './helpers'

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

test.describe('Git LFS visuals', () => {
  test.beforeEach(async ({ page }) => {
    await page.addInitScript(() => {
      document.cookie = 'ogb-site-gate-unlocked=1; Path=/; SameSite=Lax'
    })
  })

  test('gallery LFS image preview', async ({ page }) => {
    await waitForApp(page)
    const section = page.getByTestId('visual-lfs-blob-image')
    await expect(section.getByTestId('lfs-badge')).toBeVisible()
    await expect(section.locator('img')).toHaveJSProperty('complete', true)
    await expect(section).toHaveScreenshot('visual-lfs-blob-image.png')
  })

  test('gallery LFS file download only', async ({ page }) => {
    await waitForApp(page)
    await expect(page.getByTestId('visual-lfs-blob-download')).toHaveScreenshot('visual-lfs-blob-download.png')
  })

  test('gallery LFS object not available', async ({ page }) => {
    await waitForApp(page)
    const section = page.getByTestId('visual-lfs-blob-unavailable')
    await expect(section.getByTestId('lfs-not-available')).toContainText('LFS object not available')
    await expect(section).toHaveScreenshot('visual-lfs-blob-unavailable.png')
  })

  test('gallery storage meter with Git LFS', async ({ page }) => {
    await waitForApp(page)
    const section = page.getByTestId('visual-storage-meter-lfs')
    await expect(section.getByTestId('storage-usage-split')).toContainText('Git LFS: 288.0 MB')
    await expect(section).toHaveScreenshot('visual-storage-meter-lfs.png')
  })
})
