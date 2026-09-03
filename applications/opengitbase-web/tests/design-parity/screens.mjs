// Shared screen catalogue for the design-parity harness.
// Each screen maps a live route to its Terminal/Ice mockup of the same id.
// Mockup source of truth: docs/wireframes/directions/terminal/ice/<id>.dc.html

export const WIDTH = 1360

export const SCREENS = [
  {
    id: 'Home', label: 'Home (logged out)', route: '/', auth: 'out',
    note: 'Marketing masthead. The mockup shows a fuller hero + feature grid; the live landing is the currently-implemented version.',
  },
  {
    id: 'Dashboard', label: 'Dashboard', route: '/', auth: 'user',
    note: 'Logged-in home — stat tiles, repository list, activity feed.',
  },
  {
    id: 'RepoOverview', label: 'Repo overview', route: '/demo-user/hello-world', auth: 'user',
    note: 'The live overview is sparser (ref bar + clone). The extra rails in the mockup are not-yet-built features, not styling gaps.',
  },
  {
    id: 'RepoFiles', label: 'Repo files', route: '/demo-user/hello-world/tree/main', auth: 'user',
    note: 'File tree + blob view. Compare mono code styling and the line-number gutter.',
  },
  {
    id: 'MergeRequest', label: 'Merge request', route: '/demo-user/hello-world/merge-requests/7', auth: 'out',
    mock: 'mergeRequest',
    note: 'Diff hunk, review comments, reviewers. Rendered with the same fixtures the visual suite uses (MR #7, public repo, anonymous viewer).',
  },
  {
    id: 'Pipeline', label: 'Pipeline', route: '/demo-user/hello-world/pipelines/99999999-0000-0000-0000-000000000001', auth: 'user',
    note: 'CI run detail — stage rows, running-state accent, log panel.',
  },
  {
    id: 'Discussion', label: 'Discussion', route: '/demo-user/hello-world/discussions/1', auth: 'user',
    note: 'Issue thread + details sidebar. Compare comment cards and label badges.',
  },
  {
    id: 'AdminStorage', label: 'Admin · storage', route: '/admin/storage', auth: 'admin',
    note: 'Storage fleet ops. The mockup shows a populated node table; the live mock has no nodes, so the real screen renders its empty state.',
  },
  {
    id: 'Settings', label: 'Repo settings', route: '/demo-user/hello-world/settings', auth: 'user',
    note: 'Config, storage, branch/discussion links, and the red danger zone.',
  },
]

// --- extra route fixtures for screens whose detail endpoints aren't in the public MSW mock ---
export function installExtraRoutes(page, id) {
  if (id !== 'MergeRequest') return Promise.resolve()
  const repo = {
    id: '11111111-1111-1111-1111-111111111111', name: 'Hello World', slug: 'hello-world',
    ownerUserId: '22222222-2222-2222-2222-222222222222', ownerSlug: 'demo-user', isPrivate: false, updatedAt: '2026-06-01T12:00:00Z',
  }
  return Promise.all([
    page.route('**/api/repository/by-slug/demo-user/hello-world/merge-requests/7', r => r.fulfill({ json: {
      id: 'mr-7', repositoryId: repo.id, number: 7, title: 'Refactor branch policy editor',
      body: 'This merge request adds reusable policy controls.', status: 'Open', isDraft: false,
      creatorUserId: repo.ownerUserId, creatorUsername: 'demo-user', sourceRef: 'feature/branch-rules', targetRef: 'main',
      sourceHeadSha: 'abc123def456', targetBaseSha: 'fff000', createdAt: '2026-06-27T08:00:00.000Z', updatedAt: '2026-06-27T09:00:00.000Z',
    } })),
    page.route('**/api/repository/by-slug/demo-user/hello-world/merge-requests/7/changes', r => r.fulfill({ json: { files: [
      { filePath: 'src/policy.ts', changeType: 'modified', hunks: [{ header: '@@ -10,2 +10,3 @@', lines: [
        { oldLineNumber: 10, newLineNumber: 10, type: 'context', content: 'const allowed = rules.filter(Boolean)' },
        { oldLineNumber: 11, newLineNumber: null, type: 'remove', content: 'return allowed.some(isAllowed)' },
        { oldLineNumber: null, newLineNumber: 11, type: 'add', content: 'return allowed.some(rule => rule.matches(ref))' },
        { oldLineNumber: null, newLineNumber: 12, type: 'add', content: '  && !isBlockedUser(userId)' },
      ] }] },
    ] } })),
    page.route('**/api/repository/by-slug/demo-user/hello-world/merge-requests/7/commits', r => r.fulfill({ json: [
      { sha: 'abc123def456', shortSha: 'abc123de', message: 'refactor protected branch policy editor', authorName: 'demo-user', authoredAt: '2026-06-27T08:40:00.000Z' },
    ] })),
    page.route('**/api/repository/by-slug/demo-user/hello-world/merge-requests/7/comments**', r => r.fulfill({ json: [
      { id: 'overview-root-1', mergeRequestId: 'mr-7', authorUserId: '33333333-3333-3333-3333-333333333333', authorUsername: 'reviewer',
        bodyMarkdown: 'Looks good overall. One nit in the changes tab.', createdAt: '2026-06-27T09:00:00.000Z', updatedAt: '2026-06-27T09:00:00.000Z',
        isDeleted: false, isResolved: false, isOutdated: false, replyCount: 0, parentCommentId: null, replies: [] },
    ] })),
    page.route('**/api/repository/by-slug/demo-user/hello-world/merge-requests/7/discussion-links**', r => r.fulfill({ json: [] })),
    page.route('**/api/repository/by-slug/demo-user/hello-world/discussions?*', r => r.fulfill({ json: [] })),
    page.route('**/api/repository/by-slug/demo-user/hello-world', r => r.fulfill({ json: repo })),
    page.route('**/api/repository-member/**', r => r.fulfill({ json: [] })),
  ])
}
