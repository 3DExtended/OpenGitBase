import { test, expect } from '@playwright/test'

const baseURL = process.env.PLAYWRIGHT_BASE_URL ?? 'http://localhost:3000'

const mockDiscussionDetail = {
  id: 'disc-0001-0000-0000-000000000001',
  repositoryId: '11111111-1111-1111-1111-111111111111',
  number: 1,
  title: 'Architecture review',
  body: null,
  status: 'Open',
  hasEverBeenEngaged: true,
  creatorUserId: '22222222-2222-2222-2222-222222222222',
  creatorUsername: 'demo-user',
  assigneeUserId: null,
  createdAt: '2026-06-24T10:00:00.000Z',
  updatedAt: '2026-06-24T11:00:00.000Z',
  tags: [],
  comments: [
    {
      id: 'comment-root-1',
      discussionId: 'disc-0001-0000-0000-000000000001',
      authorUserId: '22222222-2222-2222-2222-222222222222',
      authorUsername: 'demo-user',
      bodyMarkdown: 'Consider extracting this helper.',
      createdAt: '2026-06-24T10:00:00.000Z',
      updatedAt: '2026-06-24T10:00:00.000Z',
      isDeleted: false,
      isResolved: false,
      replyCount: 1,
      orphanedFromDeletedRoot: false,
      replies: [
        {
          id: 'comment-reply-1',
          discussionId: 'disc-0001-0000-0000-000000000001',
          authorUserId: '33333333-3333-3333-3333-333333333333',
          authorUsername: 'reviewer',
          bodyMarkdown: 'Agreed — I pushed a follow-up snippet.',
          createdAt: '2026-06-24T10:05:00.000Z',
          updatedAt: '2026-06-24T10:05:00.000Z',
          isDeleted: false,
          isResolved: false,
          replyCount: 0,
          orphanedFromDeletedRoot: false,
          replies: [],
        },
      ],
    },
  ],
}

async function installDiscussionRoutes(page: import('@playwright/test').Page) {
  await page.route('**/api/repository/by-slug/**/discussions/1**', async (route) => {
    const url = new URL(route.request().url())
    if (url.pathname.endsWith('/comments')) {
      await route.fulfill({ json: mockDiscussionDetail.comments })
      return
    }
    if (url.searchParams.get('include') === 'comments') {
      await route.fulfill({ json: mockDiscussionDetail })
      return
    }
    const { comments: _comments, ...withoutComments } = mockDiscussionDetail
    await route.fulfill({ json: withoutComments })
  })

  await page.route('**/api/repository/by-slug/demo-user/hello-world', async (route) => {
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
    await route.fulfill({
      json: [
        {
          id: '88888888-8888-8888-8888-888888888888',
          repositoryId: '11111111-1111-1111-1111-111111111111',
          userId: '22222222-2222-2222-2222-222222222222',
          username: 'demo-user',
          role: 2,
        },
      ],
    })
  })
}

async function waitForApp(page: import('@playwright/test').Page) {
  await page.addInitScript(() => {
    localStorage.setItem('ogb-site-gate-unlocked', '1')
    void navigator.serviceWorker.getRegistrations().then(registrations =>
      Promise.all(registrations.map(registration => registration.unregister())),
    )
  })
  await installDiscussionRoutes(page)
  await page.goto(`${baseURL}/demo-user/hello-world/discussions/1`)
  await page.waitForLoadState('networkidle')
}

test.describe('Discussion detail page', () => {
  test('renders bundled comment threads', async ({ page }) => {
    await waitForApp(page)

    await expect(page.getByRole('heading', { name: 'Architecture review' })).toBeVisible()
    await expect(page.getByTestId('discussion-sub-thread')).toHaveCount(1)
    await expect(page.getByTestId('discussion-sub-thread-reply')).toHaveCount(1)
    await expect(page.getByText('Consider extracting this helper.')).toBeVisible()
    await expect(page.getByText('Agreed — I pushed a follow-up snippet.')).toBeVisible()
    await expect(page.getByText('demo-user')).toBeVisible()
    await expect(page.getByText('reviewer')).toBeVisible()
    await expect(page.getByRole('button', { name: 'Resolve thread' })).toBeVisible()
    await expect(page.getByText('Creator').locator('..').getByText('demo-user')).toBeVisible()
    await expect(page.locator('time[datetime]').first()).toBeVisible()
  })
})
