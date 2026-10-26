import { PAGE_READY_TIMEOUT, expect, test } from './helpers'

test.describe('Admin CI console @visual', () => {
  test('admin ci page', async ({ page }) => {
    await page.goto('/__visual__/admin-ci')
    await expect(page.getByTestId('visual-admin-ci')).toBeVisible({ timeout: PAGE_READY_TIMEOUT })
    await expect(page).toHaveScreenshot('admin-ci.png')
  })
})
