import { test, expect } from '@playwright/test'

const mockUser = {
  userId: '22222222-2222-2222-2222-222222222222',
  username: 'demo-user',
  emailVerified: false,
  isAdmin: false,
}

const mockOrg = {
  id: '44444444-4444-4444-4444-444444444444',
  name: 'Acme Corp',
}

const mockOrgMembers = [
  {
    id: '66666666-6666-6666-6666-666666666666',
    organizationId: mockOrg.id,
    userId: mockUser.userId,
    username: mockUser.username,
    role: 1,
  },
]

async function mockAuthenticatedOrgApis(page: import('@playwright/test').Page) {
  await page.route('**/api/account/me', route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(mockUser) }),
  )
  await page.route('**/api/organization', route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([mockOrg]) }),
  )
  await page.route('**/api/organization/by-slug/acme-corp', route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({ ...mockOrg, slug: 'acme-corp' }),
    }),
  )
  await page.route('**/api/organization/*/members', route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify(mockOrgMembers) }),
  )
  await page.route('**/api/repository', route =>
    route.fulfill({ status: 200, contentType: 'application/json', body: JSON.stringify([]) }),
  )
  await page.route('**/api/public/owners/acme-corp', route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        slug: 'acme-corp',
        name: 'Acme Corp',
        kind: 'organization',
        bio: 'Building things together.',
        repositories: [],
      }),
    }),
  )
  await page.route('**/api/public/owners/opengitbase', route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        slug: 'opengitbase',
        name: 'OpenGitBase',
        kind: 'organization',
        bio: 'Official org.',
        repositories: [],
      }),
    }),
  )
}

test.beforeEach(async ({ page }) => {
  await page.addInitScript(() => {
    localStorage.setItem('ogb-site-gate-unlocked', '1')
  })
})

test('org sidebar shows members when org display name differs from slug', async ({ page }) => {
  await mockAuthenticatedOrgApis(page)
  await page.goto('/acme-corp')
  await page.waitForLoadState('networkidle')
  await expect(page.getByRole('link', { name: 'Sign in' })).toHaveCount(0)

  const sidebar = page.getByTestId('sidebar-panel')
  await expect(sidebar).toBeVisible()
  await expect(sidebar.getByRole('link', { name: 'Members' })).toBeVisible()
})

test('reserved seeded org slug opengitbase uses org sidebar', async ({ page }) => {
  await mockAuthenticatedOrgApis(page)
  await page.route('**/api/organization', route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify([{
        id: '44444444-4444-4444-4444-444444444444',
        name: 'OpenGitBase',
        slug: 'opengitbase',
      }]),
    }),
  )
  await page.route('**/api/organization/by-slug/opengitbase', route =>
    route.fulfill({
      status: 200,
      contentType: 'application/json',
      body: JSON.stringify({
        id: '44444444-4444-4444-4444-444444444444',
        name: 'OpenGitBase',
        slug: 'opengitbase',
      }),
    }),
  )

  await page.goto('/opengitbase')
  await page.waitForLoadState('networkidle')

  const sidebar = page.getByTestId('sidebar-panel')
  await expect(sidebar).toBeVisible()
  await expect(sidebar.getByRole('link', { name: 'Members' })).toBeVisible()
})
