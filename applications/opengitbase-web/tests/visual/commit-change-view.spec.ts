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

const commitDetailPayload = {
  sha: 'abc123def4567890abcdef1234567890abcdef12',
  shortSha: 'abc123de',
  message: 'refactor protected branch policy editor',
  authorName: 'demo-user',
  authoredAt: '2026-06-27T08:40:00.000Z',
  parents: [
    {
      sha: 'fff0000000000000000000000000000000000001',
      shortSha: 'fff00000',
    },
  ],
  stats: {
    filesChanged: 1,
    insertions: 2,
    deletions: 1,
  },
  kind: 'diff',
  diffFiles: [
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
          ],
        },
      ],
    },
  ],
  rootFiles: [],
}

async function installCommitRoutes(page: import('@playwright/test').Page) {
  await page.route('**/api/account/me**', async (route) => {
    await route.fulfill({ status: 401, body: '' })
  })

  await page.route('**/api/repository/by-slug/demo-user/hello-world/commits/**', async (route) => {
    await route.fulfill({ json: commitDetailPayload })
  })

  await page.route('**/api/repository/by-slug/demo-user/hello-world/merge-requests/7/discussion-links**', async (route) => {
    await route.fulfill({ json: [] })
  })

  await page.route('**/api/repository/by-slug/demo-user/hello-world/merge-requests/7/comments**', async (route) => {
    await route.fulfill({ json: [] })
  })

  await page.route('**/api/repository/by-slug/demo-user/hello-world/merge-requests/7/changes**', async (route) => {
    await route.fulfill({ json: { files: commitDetailPayload.diffFiles } })
  })

  await page.route('**/api/repository/by-slug/demo-user/hello-world/merge-requests/7/commits', async (route) => {
    await route.fulfill({
      json: [
        {
          sha: commitDetailPayload.sha,
          shortSha: commitDetailPayload.shortSha,
          message: commitDetailPayload.message,
          authorName: commitDetailPayload.authorName,
          authoredAt: commitDetailPayload.authoredAt,
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
        sourceHeadSha: commitDetailPayload.sha,
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

test.describe('Commit change view', () => {
  test.beforeEach(async ({ page }) => {
    await page.addInitScript(() => {
      localStorage.setItem('ogb-site-gate-unlocked', '1')
      void navigator.serviceWorker.getRegistrations().then(registrations =>
        Promise.all(registrations.map(registration => registration.unregister())),
      )
    })
  })

  test('gallery unified diff fixture', async ({ page }) => {
    await page.goto(`${baseURL}/__visual__/?msw=1`)
    await page.waitForLoadState('networkidle')
    await disableAnimations(page)
    await expect(page.getByTestId('visual-commit-unified-diff')).toHaveScreenshot('visual-commit-unified-diff.png')
  })

  test('commit page renders diff', async ({ page }) => {
    await installCommitRoutes(page)
    await page.goto(`${baseURL}/demo-user/hello-world/commit/abc123de?from=mr/7`, { waitUntil: 'domcontentloaded' })
    await expect(page.getByRole('heading', { name: commitDetailPayload.message })).toBeVisible()
    await expect(page.getByText('src/policy.ts')).toBeVisible()
    await disableAnimations(page)
    await expect(page.locator('body')).toHaveScreenshot('commit-change-view-page.png', { fullPage: true })
  })

  test('click-through from merge request commits tab', async ({ page }) => {
    await installCommitRoutes(page)
    await page.goto(`${baseURL}/demo-user/hello-world/merge-requests/7`, { waitUntil: 'domcontentloaded' })
    await page.getByRole('tab', { name: 'Commits' }).click()
    await page.getByText(commitDetailPayload.message).click()
    await expect(page).toHaveURL(/\/demo-user\/hello-world\/commit\//)
    await expect(page.getByRole('heading', { name: commitDetailPayload.message })).toBeVisible()
    await expect(page.getByRole('link', { name: 'Back to merge request !7' })).toBeVisible()
  })
})
