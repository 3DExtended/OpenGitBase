import { expect, test } from '@playwright/test'

const baseURL = process.env.PLAYWRIGHT_BASE_URL ?? 'http://localhost:3100'

type DiscussionLink = {
  discussionNumber: number
  relationshipType: string
  discussionTitle: string
  discussionStatus: string
}

async function disableAnimations(page: import('@playwright/test').Page) {
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

async function waitForMergeRequestPage(page: import('@playwright/test').Page) {
  await expect(page.getByRole('heading', { name: 'Refactor branch policy editor' })).toBeVisible()
  await expect(page.getByTestId('mr-linked-discussions')).toBeVisible()
}

async function installLinkedDiscussionsRoutes(
  page: import('@playwright/test').Page,
  options: { authenticated?: boolean } = {},
) {
  const authenticated = options.authenticated ?? false
  await page.route('**/api/account/me**', async (route) => {
    if (!authenticated) {
      await route.fulfill({ status: 401, body: '' })
      return
    }
    await route.fulfill({
      json: {
        userId: '22222222-2222-2222-2222-222222222222',
        username: 'demo-user',
        emailVerified: true,
        isAdmin: false,
      },
    })
  })

  const links: DiscussionLink[] = [
    {
      discussionNumber: 12,
      relationshipType: 'closes',
      discussionTitle: 'Protect default branch',
      discussionStatus: 'Open',
    },
    {
      discussionNumber: 5,
      relationshipType: 'implements',
      discussionTitle: 'Policy matcher refactor',
      discussionStatus: 'Open',
    },
  ]

  await page.route('**/api/repository/by-slug/demo-user/hello-world/merge-requests/7/discussion-links**', async (route) => {
    const method = route.request().method()
    if (method === 'GET') {
      await route.fulfill({ json: links })
      return
    }
    if (method === 'POST') {
      const body = route.request().postDataJSON() as { discussionNumber: number, relationshipType: string }
      links.push({
        discussionNumber: body.discussionNumber,
        relationshipType: body.relationshipType,
        discussionTitle: 'Add integration tests',
        discussionStatus: 'Open',
      })
      await route.fulfill({
        status: 201,
        json: links.at(-1),
      })
      return
    }
    if (method === 'DELETE') {
      const url = new URL(route.request().url())
      const discussionNumber = Number(url.pathname.split('/').pop())
      const relationshipType = url.searchParams.get('relationshipType')
      const index = links.findIndex(link =>
        link.discussionNumber === discussionNumber
        && link.relationshipType.toLowerCase() === relationshipType?.toLowerCase(),
      )
      if (index >= 0) {
        links.splice(index, 1)
      }
      await route.fulfill({ status: 204, body: '' })
      return
    }
    await route.continue()
  })

  await page.route('**/api/repository/by-slug/demo-user/hello-world/merge-requests/7/comments**', async (route) => {
    await route.fulfill({ json: [] })
  })

  await page.route('**/api/repository/by-slug/demo-user/hello-world/merge-requests/7/changes', async (route) => {
    await route.fulfill({ json: { files: [] } })
  })

  await page.route('**/api/repository/by-slug/demo-user/hello-world/merge-requests/7/commits', async (route) => {
    await route.fulfill({ json: [] })
  })

  await page.route('**/api/repository/by-slug/demo-user/hello-world/merge-requests/7', async (route) => {
    await route.fulfill({
      json: {
        id: 'mr-7',
        repositoryId: '11111111-1111-1111-1111-111111111111',
        number: 7,
        title: 'Refactor branch policy editor',
        body: 'This merge request adds reusable policy controls.',
        status: 'Open',
        isDraft: false,
        creatorUserId: '22222222-2222-2222-2222-222222222222',
        creatorUsername: 'demo-user',
        sourceRef: 'feature/branch-rules',
        targetRef: 'main',
        sourceHeadSha: 'abc123def456',
        targetBaseSha: 'fff000',
        createdAt: '2026-06-27T08:00:00.000Z',
        updatedAt: '2026-06-27T09:00:00.000Z',
      },
    })
  })

  await page.route(/\/api\/repository\/by-slug\/demo-user\/hello-world\/discussions(\?.*)?$/, async (route) => {
    await route.fulfill({
      json: [
        {
          id: 'disc-2',
          repositoryId: '11111111-1111-1111-1111-111111111111',
          number: 2,
          title: 'Add integration tests',
          status: 'Open',
          hasEverBeenEngaged: false,
          creatorUserId: '22222222-2222-2222-2222-222222222222',
          creatorUsername: 'demo-user',
          createdAt: '2026-06-27T08:00:00.000Z',
          updatedAt: '2026-06-27T08:00:00.000Z',
          tags: [],
        },
        {
          id: 'disc-9',
          repositoryId: '11111111-1111-1111-1111-111111111111',
          number: 9,
          title: 'README cleanup',
          status: 'Open',
          hasEverBeenEngaged: false,
          creatorUserId: '22222222-2222-2222-2222-222222222222',
          creatorUsername: 'demo-user',
          createdAt: '2026-06-27T08:00:00.000Z',
          updatedAt: '2026-06-27T08:00:00.000Z',
          tags: [],
        },
      ],
    })
  })

  await page.route(/\/api\/repository\/by-slug\/demo-user\/hello-world$/, async (route) => {
    await route.fulfill({
      json: {
        id: '11111111-1111-1111-1111-111111111111',
        name: 'Hello World',
        slug: 'hello-world',
        ownerUserId: '22222222-2222-2222-2222-222222222222',
        ownerSlug: 'demo-user',
        isPrivate: false,
        updatedAt: '2026-06-01T12:00:00Z',
      },
    })
  })

  await page.route('**/api/repository-member/**', async (route) => {
    await route.fulfill({ json: [] })
  })
}

test.describe('Merge request linked discussions @regression', () => {
  test.beforeEach(async ({ page }) => {
    await page.addInitScript(() => {
      localStorage.setItem('ogb-site-gate-unlocked', '1')
      void navigator.serviceWorker.getRegistrations().then(registrations =>
        Promise.all(registrations.map(registration => registration.unregister())),
      )
    })
  })

  test('renders grouped linked discussions sidebar', async ({ page }) => {
    await installLinkedDiscussionsRoutes(page)
    await page.goto(`${baseURL}/demo-user/hello-world/merge-requests/7`, { waitUntil: 'domcontentloaded' })
    await waitForMergeRequestPage(page)
    await disableAnimations(page)

    await expect(page.getByTestId('mr-linked-discussion-group-closes')).toBeVisible()
    await expect(page.getByTestId('mr-linked-discussion-group-implements')).toBeVisible()
    await expect(page.getByTestId('mr-linked-discussions')).toContainText('#12 Protect default branch')
    await expect(page.getByTestId('mr-linked-discussions')).toContainText('#5 Policy matcher refactor')
    await expect(page.getByTestId('mr-link-discussion-expand')).toHaveCount(0)
  })

  test('filters open discussions in accordion picker', async ({ page }) => {
    await installLinkedDiscussionsRoutes(page, { authenticated: true })
    await page.goto(`${baseURL}/demo-user/hello-world/merge-requests/7`, { waitUntil: 'domcontentloaded' })
    await waitForMergeRequestPage(page)
    await disableAnimations(page)

    await page.getByTestId('mr-link-discussion-expand').click()
    await expect(page.getByTestId('mr-discussion-link-picker').getByRole('button', { name: /#2 Add integration tests/i })).toBeVisible()
    await page.getByTestId('mr-linked-discussions').getByPlaceholder(/filter open discussions/i).fill('README')
    await expect(page.getByTestId('mr-discussion-link-picker').getByRole('button', { name: /#9 README cleanup/i })).toBeVisible()
    await expect(page.getByTestId('mr-discussion-link-picker').getByRole('button', { name: /#2 Add integration tests/i })).toHaveCount(0)
  })

  test('links another discussion from accordion picker', async ({ page }) => {
    await installLinkedDiscussionsRoutes(page, { authenticated: true })
    await page.goto(`${baseURL}/demo-user/hello-world/merge-requests/7`, { waitUntil: 'domcontentloaded' })
    await waitForMergeRequestPage(page)
    await disableAnimations(page)

    await page.getByTestId('mr-link-discussion-expand').click()
    await expect(page.getByTestId('mr-discussion-link-picker')).toBeVisible()
    await page.getByTestId('mr-discussion-link-picker').getByRole('button', { name: /#2 Add integration tests/i }).click()

    await expect(page.getByTestId('mr-linked-discussions')).toContainText('#2 Add integration tests')
  })

  test('removes a linked discussion', async ({ page }) => {
    await installLinkedDiscussionsRoutes(page, { authenticated: true })
    await page.goto(`${baseURL}/demo-user/hello-world/merge-requests/7`, { waitUntil: 'domcontentloaded' })
    await waitForMergeRequestPage(page)
    await disableAnimations(page)

    await page.getByTestId('mr-linked-discussion-group-closes').getByRole('button', { name: 'Remove discussion link' }).click()
    await expect(page.getByTestId('mr-linked-discussions')).not.toContainText('#12 Protect default branch')
  })
})
