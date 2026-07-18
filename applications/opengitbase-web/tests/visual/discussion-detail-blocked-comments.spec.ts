import { test, expect } from '@playwright/test'

const baseURL = process.env.PLAYWRIGHT_BASE_URL ?? 'http://localhost:3100'

const mockDiscussionDetail = {
  id: 'disc-0001-0000-0000-000000000001',
  repositoryId: '11111111-1111-1111-1111-111111111111',
  number: 1,
  title: 'Architecture review',
  body: null,
  status: 'Open',
  hasEverBeenEngaged: true,
  creatorUserId: '22222222-2222-2222-2222-222222222222',
  assigneeUserId: null,
  createdAt: '2026-06-24T10:00:00.000Z',
  updatedAt: '2026-06-24T11:00:00.000Z',
  tags: [],
  comments: [
    {
      id: 'comment-root-1',
      discussionId: 'disc-0001-0000-0000-000000000001',
      authorUserId: '22222222-2222-2222-2222-222222222222',
      bodyMarkdown: 'Consider extracting this helper.',
      createdAt: '2026-06-24T10:00:00.000Z',
      updatedAt: '2026-06-24T10:00:00.000Z',
      isDeleted: false,
      isResolved: false,
      replyCount: 0,
      orphanedFromDeletedRoot: false,
      replies: [],
    },
    {
      id: 'comment-root-2',
      discussionId: 'disc-0001-0000-0000-000000000001',
      authorUserId: '33333333-3333-3333-3333-333333333333',
      bodyMarkdown: 'Second bundled thread.',
      createdAt: '2026-06-24T10:10:00.000Z',
      updatedAt: '2026-06-24T10:10:00.000Z',
      isDeleted: false,
      isResolved: false,
      replyCount: 0,
      orphanedFromDeletedRoot: false,
      replies: [],
    },
  ],
}

async function installBlockedCommentsRoutes(page: import('@playwright/test').Page) {
  let commentsRequestCount = 0

  await page.route('**/api/repository/by-slug/**/discussions/**/comments', async (route) => {
    commentsRequestCount += 1
    await route.abort('blockedbyclient')
  })

  await page.route('**/api/repository/by-slug/**/discussions/1**', async (route) => {
    const url = new URL(route.request().url())
    if (url.pathname.endsWith('/comments')) {
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
    await route.fulfill({ json: [] })
  })

  return {
    getCommentsRequestCount: () => commentsRequestCount,
  }
}

test.describe('Discussion detail page blocked /comments path', () => {
  test('still renders bundled threads when /comments is blocked', async ({ page }) => {
    await page.addInitScript(() => {
      document.cookie = 'ogb-site-gate-unlocked=1; Path=/; SameSite=Lax'
      void navigator.serviceWorker.getRegistrations().then(registrations =>
        Promise.all(registrations.map(registration => registration.unregister())),
      )
    })

    const routes = await installBlockedCommentsRoutes(page)
    await page.goto(`${baseURL}/demo-user/hello-world/discussions/1`)
    await page.waitForLoadState('networkidle')

    await expect(page.getByRole('heading', { name: 'Architecture review' })).toBeVisible()
    await expect(page.getByTestId('discussion-sub-thread')).toHaveCount(2)
    await expect(page.getByText('Consider extracting this helper.')).toBeVisible()
    await expect(page.getByText('Second bundled thread.')).toBeVisible()
    await expect(page.getByText('No comments yet.')).toHaveCount(0)
    expect(routes.getCommentsRequestCount()).toBe(0)
  })
})
