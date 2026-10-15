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

const reviewerReply = {
  id: 'comment-spec-reply-1',
  discussionId: 'disc-0001-0000-0000-000000000001',
  authorUserId: '33333333-3333-3333-3333-333333333333',
  authorUsername: 'reviewer',
  bodyMarkdown: 'Do we need a migration for the existing rows?',
  createdAt: '2026-06-24T12:00:00.000Z',
  updatedAt: '2026-06-24T12:00:00.000Z',
  isDeleted: false,
  isResolved: false,
  replyCount: 0,
  orphanedFromDeletedRoot: false,
  replies: [],
}

// Created with a body, which the API echoes as the creator's opening comment; that comment was edited later.
const mockEditedSpecDetail = {
  ...mockDiscussionDetail,
  title: 'Rate limit spec',
  body: '## Goal\n\nOriginal spec: limit pushes to 10 per minute.',
  comments: [
    {
      id: 'comment-spec-opening',
      discussionId: 'disc-0001-0000-0000-000000000001',
      authorUserId: '22222222-2222-2222-2222-222222222222',
      authorUsername: 'demo-user',
      bodyMarkdown: '## Goal\n\nRevised spec: limit pushes to 30 per minute.',
      createdAt: '2026-06-24T10:00:00.000Z',
      updatedAt: '2026-06-24T11:30:00.000Z',
      editedAt: '2026-06-24T11:30:00.000Z',
      isDeleted: false,
      isResolved: false,
      replyCount: 0,
      orphanedFromDeletedRoot: false,
      replies: [],
    },
    reviewerReply,
  ],
}

// Created with a body but no opening echo by the creator, so the body is the only copy of the text.
const mockUnechoedBodyDetail = {
  ...mockDiscussionDetail,
  title: 'Rate limit spec',
  body: '## Goal\n\nOriginal spec: limit pushes to 10 per minute.',
  comments: [reviewerReply],
}

type MockDiscussionDetail = Omit<typeof mockDiscussionDetail, 'body' | 'comments'> & {
  body: string | null
  comments: unknown[]
}

async function installDiscussionRoutes(
  page: import('@playwright/test').Page,
  detail: MockDiscussionDetail,
) {
  await page.route('**/api/repository/by-slug/**/discussions/1**', async (route) => {
    const url = new URL(route.request().url())
    if (url.pathname.endsWith('/comments')) {
      await route.fulfill({ json: detail.comments })
      return
    }
    if (url.pathname.endsWith('/links') || url.pathname.endsWith('/linked-merge-requests')) {
      await route.fulfill({ json: [] })
      return
    }
    if (url.searchParams.get('include') === 'comments') {
      await route.fulfill({ json: detail })
      return
    }
    const { comments: _comments, ...withoutComments } = detail
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

async function waitForApp(
  page: import('@playwright/test').Page,
  detail: MockDiscussionDetail = mockDiscussionDetail,
) {
  await page.addInitScript(() => {
    document.cookie = 'ogb-site-gate-unlocked=1; Path=/; SameSite=Lax'
    void navigator.serviceWorker.getRegistrations().then(registrations =>
      Promise.all(registrations.map(registration => registration.unregister())),
    )
  })
  await installDiscussionRoutes(page, detail)
  await page.goto(`${baseURL}/demo-user/hello-world/discussions/1`)
  await page.waitForLoadState('networkidle')
}

async function settleForScreenshot(page: import('@playwright/test').Page) {
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

test.describe('Discussion detail page', () => {
  test('renders bundled comment threads', async ({ page }) => {
    await waitForApp(page)

    await expect(page.getByRole('heading', { name: 'Architecture review' })).toBeVisible()
    await expect(page.getByTestId('discussion-sub-thread')).toHaveCount(1)
    await expect(page.getByTestId('discussion-sub-thread-reply')).toHaveCount(1)
    await expect(page.getByText('Consider extracting this helper.')).toBeVisible()
    await expect(page.getByText('Agreed — I pushed a follow-up snippet.')).toBeVisible()
    await expect(page.getByTestId('discussion-sub-thread').getByText('demo-user')).toBeVisible()
    await expect(page.getByText('reviewer')).toBeVisible()
    await expect(page.getByText('Creator').locator('..').getByText('demo-user')).toBeVisible()
    await expect(page.locator('time[datetime]').first()).toBeVisible()
  })

  test('shows the edited opening comment instead of the stale body', async ({ page }) => {
    await page.clock.setFixedTime(new Date('2026-06-25T12:00:00.000Z'))
    await waitForApp(page, mockEditedSpecDetail)

    await expect(page.getByRole('heading', { name: 'Rate limit spec' })).toBeVisible()
    await expect(page.getByText('Revised spec: limit pushes to 30 per minute.')).toHaveCount(1)
    await expect(page.getByText('Original spec: limit pushes to 10 per minute.')).toHaveCount(0)
    await expect(page.getByTestId('discussion-body')).toHaveCount(0)
    await expect(page.getByTestId('discussion-sub-thread')).toHaveCount(2)

    await settleForScreenshot(page)
    await expect(page.getByTestId('discussion-detail-main')).toHaveScreenshot('discussion-detail-edited-spec.png')
  })

  test('keeps the body when no opening comment echoes it', async ({ page }) => {
    await page.clock.setFixedTime(new Date('2026-06-25T12:00:00.000Z'))
    await waitForApp(page, mockUnechoedBodyDetail)

    await expect(page.getByTestId('discussion-body')).toContainText('Original spec: limit pushes to 10 per minute.')
    await expect(page.getByTestId('discussion-sub-thread')).toHaveCount(1)

    await settleForScreenshot(page)
    await expect(page.getByTestId('discussion-detail-main')).toHaveScreenshot('discussion-detail-unechoed-body.png')
  })
})
