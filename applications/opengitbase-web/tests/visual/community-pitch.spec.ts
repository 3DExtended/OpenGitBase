import { test, expect } from '@playwright/test'

test.beforeEach(async ({ page }) => {
  await page.addInitScript(() => {
    localStorage.setItem('ogb-site-gate-unlocked', '1')
    void navigator.serviceWorker.getRegistrations().then(registrations =>
      Promise.all(registrations.map(registration => registration.unregister())),
    )
  })
})

test.describe('Community pitch @regression', () => {
  test('loads title slide and primary CTA links', async ({ page }) => {
    await page.goto('/pitch')
    await page.waitForLoadState('networkidle')
    await page.waitForSelector('[data-testid="community-pitch-deck"]')

    await expect(page.getByTestId('community-pitch')).toBeVisible()
    await expect(page.locator('.pitch-slide-title h1')).toContainText(
      /yours to design/i,
    )

    const issuesLink = page.locator('a.pitch-link-primary', {
      hasText: 'GitHub Issues',
    })
    await expect(issuesLink).toHaveAttribute(
      'href',
      'https://github.com/3DExtended/OpenGitBase/issues',
    )
  })

  test('run locally slide links to hosted README', async ({ page }) => {
    await page.goto('/pitch#/cta-run')
    await page.waitForLoadState('networkidle')
    await page.waitForSelector('#cta-run')

    const readmeLink = page.locator('a.pitch-link-primary', {
      hasText: 'Setup guide (README)',
    })
    await expect(readmeLink).toHaveAttribute(
      'href',
      'https://www.opengitbase.com/opengitbase/open-git-base',
    )
  })

  test('header exposes Community nav link on home', async ({ page }, testInfo) => {
    test.skip(testInfo.project.name !== 'desktop', 'Header nav links are desktop-only')
    await page.route('**/api/account/me', route =>
      route.fulfill({ status: 401, body: '' }),
    )
    await page.goto('/')
    await page.waitForLoadState('networkidle')

    await expect(
      page.getByRole('link', { name: 'Community', exact: true }),
    ).toHaveAttribute('href', '/pitch')
  })

  test('exit control returns to home', async ({ page }) => {
    await page.goto('/pitch')
    await page.waitForLoadState('networkidle')
    await page.getByRole('link', { name: 'Exit' }).click()
    await expect(page).toHaveURL('/')
  })
})
