import { expect, test } from '@playwright/test'

const baseURL = process.env.PLAYWRIGHT_BASE_URL ?? 'http://localhost:3100'

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

async function installMergeRequestRoutes(page: import('@playwright/test').Page) {
  await page.route('**/api/account/me**', async (route) => {
    await route.fulfill({ status: 401, body: '' })
  })

  await page.route('**/api/repository/by-slug/demo-user/hello-world/merge-requests/7/discussion-links**', async (route) => {
    await route.fulfill({
      json: [
        { discussionNumber: 12, relationshipType: 'closes', discussionTitle: 'Protect default branch', discussionStatus: 'Open' },
        { discussionNumber: 5, relationshipType: 'implements', discussionTitle: 'Policy matcher refactor', discussionStatus: 'Open' },
      ],
    })
  })

  await page.route('**/api/repository/by-slug/demo-user/hello-world/merge-requests/7/comments**', async (route) => {
    const url = new URL(route.request().url())
    const type = url.searchParams.get('type')
    if (type === 'review') {
      await route.fulfill({
        json: [
          {
            id: 'review-root-1',
            mergeRequestId: 'mr-7',
            authorUserId: '22222222-2222-2222-2222-222222222222',
            authorUsername: 'demo-user',
            bodyMarkdown: 'Can we simplify this branch matcher?',
            createdAt: '2026-06-27T09:00:00.000Z',
            updatedAt: '2026-06-27T09:00:00.000Z',
            isDeleted: false,
            isResolved: false,
            isOutdated: false,
            replyCount: 1,
            parentCommentId: null,
            anchor: {
              headCommitSha: 'abc123def456',
              filePath: 'src/policy.ts',
              lineNumber: 12,
              diffSide: 'new',
            },
            replies: [
              {
                id: 'review-reply-1',
                mergeRequestId: 'mr-7',
                authorUserId: '33333333-3333-3333-3333-333333333333',
                authorUsername: 'reviewer',
                bodyMarkdown: 'Yes, I can fold it into one helper.',
                createdAt: '2026-06-27T09:10:00.000Z',
                updatedAt: '2026-06-27T09:10:00.000Z',
                isDeleted: false,
                isResolved: false,
                isOutdated: false,
                replyCount: 0,
                parentCommentId: 'review-root-1',
                replies: [],
              },
            ],
          },
        ],
      })
      return
    }
    await route.fulfill({
      json: [
        {
          id: 'overview-root-1',
          mergeRequestId: 'mr-7',
          authorUserId: '33333333-3333-3333-3333-333333333333',
          authorUsername: 'reviewer',
          bodyMarkdown: 'Looks good overall. One nit in changes tab.',
          createdAt: '2026-06-27T09:00:00.000Z',
          updatedAt: '2026-06-27T09:00:00.000Z',
          isDeleted: false,
          isResolved: false,
          isOutdated: false,
          replyCount: 0,
          parentCommentId: null,
          replies: [],
        },
      ],
    })
  })

  await page.route('**/api/repository/by-slug/demo-user/hello-world/merge-requests/7/changes', async (route) => {
    await route.fulfill({
      json: {
        files: [
          {
            filePath: 'src/policy.ts',
            changeType: 'modified',
            hunks: [
              {
                header: '@@ -10,2 +10,3 @@',
                lines: [
                  { oldLineNumber: 10, newLineNumber: 10, type: 'context', content: 'const allowed = rules.filter(Boolean)' },
                  { oldLineNumber: 11, newLineNumber: null, type: 'remove', content: 'return allowed.some(isAllowed)' },
                  { oldLineNumber: null, newLineNumber: 11, type: 'add', content: 'return allowed.some(rule => rule.matches(ref))' },
                  { oldLineNumber: null, newLineNumber: 12, type: 'add', content: '  && !isBlockedUser(userId)' },
                ],
              },
            ],
          },
        ],
      },
    })
  })

  await page.route('**/api/repository/by-slug/demo-user/hello-world/merge-requests/7/commits', async (route) => {
    await route.fulfill({
      json: [
        {
          sha: 'abc123def456',
          shortSha: 'abc123de',
          message: 'refactor protected branch policy editor',
          authorName: 'demo-user',
          authoredAt: '2026-06-27T08:40:00.000Z',
        },
      ],
    })
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
}

async function installLinkedDiscussionsSidebarRoutes(page: import('@playwright/test').Page) {
  await installMergeRequestRoutes(page)
  await page.route('**/api/repository/by-slug/demo-user/hello-world/discussions?*', async (route) => {
    await route.fulfill({ json: [] })
  })
}

test.describe('Merge request visuals', () => {
  test('gallery merge request sections', async ({ page }) => {
    await page.addInitScript(() => {
      localStorage.setItem('ogb-site-gate-unlocked', '1')
    })
    await page.goto(`${baseURL}/__visual__/?msw=1`)
    await page.waitForLoadState('networkidle')
    await disableAnimations(page)
    await expect(page.getByTestId('visual-merge-requests-overview')).toHaveScreenshot('visual-merge-requests-overview.png')
    await expect(page.getByTestId('visual-merge-request-banner')).toHaveScreenshot('visual-merge-request-banner.png')
    await expect(page.getByTestId('visual-branches-settings')).toHaveScreenshot('visual-branches-settings.png')
  })

  test('merge request detail page', async ({ page }) => {
    await page.addInitScript(() => {
      localStorage.setItem('ogb-site-gate-unlocked', '1')
      void navigator.serviceWorker.getRegistrations().then(registrations =>
        Promise.all(registrations.map(registration => registration.unregister())),
      )
    })
    await installLinkedDiscussionsSidebarRoutes(page)
    await page.goto(`${baseURL}/demo-user/hello-world/merge-requests/7`, { waitUntil: 'domcontentloaded' })
    await waitForMergeRequestPage(page)
    await disableAnimations(page)
    await expect(page.locator('body')).toHaveScreenshot('merge-request-detail-overview.png', {
      fullPage: true,
    })
  })

  test('linked discussions sidebar', async ({ page }) => {
    await page.addInitScript(() => {
      localStorage.setItem('ogb-site-gate-unlocked', '1')
      void navigator.serviceWorker.getRegistrations().then(registrations =>
        Promise.all(registrations.map(registration => registration.unregister())),
      )
    })
    await installLinkedDiscussionsSidebarRoutes(page)
    await page.goto(`${baseURL}/demo-user/hello-world/merge-requests/7`, { waitUntil: 'domcontentloaded' })
    await waitForMergeRequestPage(page)
    await disableAnimations(page)
    await expect(page.getByTestId('mr-linked-discussions')).toHaveScreenshot('merge-request-linked-discussions.png')
  })
})
