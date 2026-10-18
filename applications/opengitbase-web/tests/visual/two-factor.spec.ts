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

test.describe('two-factor visuals', () => {
  test.beforeEach(async ({ page }) => {
    await page.addInitScript(() => {
      document.cookie = 'ogb-site-gate-unlocked=1; Path=/; SameSite=Lax'
    })
  })

  test('gallery two-step sign-in', async ({ page }) => {
    await waitForApp(page)
    await expect(page.getByTestId('visual-two-factor-sign-in')).toHaveScreenshot('visual-two-factor-sign-in.png')
  })

  test('gallery security status', async ({ page }) => {
    await waitForApp(page)
    await expect(page.getByTestId('visual-security-status')).toHaveScreenshot('visual-security-status.png')
  })

  test('gallery authenticator setup', async ({ page }) => {
    await waitForApp(page)
    const setup = page.getByTestId('visual-totp-setup')
    await expect(setup.getByTestId('totp-setup-key')).toHaveText('JBSW Y3DP EHPK 3PXP JBSW Y3DP EHPK 3PXP')
    await expect(setup.locator('svg').first()).toBeVisible()
    await expect(setup).toHaveScreenshot('visual-totp-setup.png')
  })
})
