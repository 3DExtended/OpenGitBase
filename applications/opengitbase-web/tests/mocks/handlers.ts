import { http, HttpResponse } from 'msw'

const mockUser = {
  userId: '22222222-2222-2222-2222-222222222222',
  username: 'demo-user',
  emailVerified: false,
  isAdmin: false,
}

const mockRepos = [
  {
    id: '11111111-1111-1111-1111-111111111111',
    name: 'Hello World',
    slug: 'hello-world',
    ownerUserId: '22222222-2222-2222-2222-222222222222',
    ownerSlug: 'demo-user',
    isPrivate: false,
    updatedAt: '2026-06-01T12:00:00Z',
  },
  {
    id: '33333333-3333-3333-3333-333333333333',
    name: 'Private Notes',
    slug: 'private-notes',
    ownerUserId: '22222222-2222-2222-2222-222222222222',
    ownerSlug: 'demo-user',
    isPrivate: true,
    updatedAt: '2026-06-10T08:30:00Z',
  },
]

const mockOrgs = [
  {
    id: '44444444-4444-4444-4444-444444444444',
    name: 'Acme Corp',
    slug: 'acme-corp',
  },
]

const mockOrgMembers = [
  {
    id: '66666666-6666-6666-6666-666666666666',
    organizationId: '44444444-4444-4444-4444-444444444444',
    userId: '22222222-2222-2222-2222-222222222222',
    username: 'demo-user',
    role: 1,
  },
]

const mockOrgInvites = [
  {
    id: '77777777-7777-7777-7777-777777777777',
    organizationId: '44444444-4444-4444-4444-444444444444',
    email: 'pe***@example.com',
    role: 0,
    invitedByUserId: '22222222-2222-2222-2222-222222222222',
    createdAt: '2026-06-01T12:00:00Z',
    expiresAt: '2026-06-08T12:00:00Z',
    status: 0,
  },
]

const mockMembers = [
  {
    id: '88888888-8888-8888-8888-888888888888',
    repositoryId: '11111111-1111-1111-1111-111111111111',
    userId: '22222222-2222-2222-2222-222222222222',
    username: 'demo-user',
    role: 2,
  },
]

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
  viewerEffectiveRole: 'Owner',
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

const mockPipelineRuns = [
  {
    id: '99999999-0000-0000-0000-000000000001',
    repositoryId: '11111111-1111-1111-1111-111111111111',
    ref: 'refs/heads/main',
    afterSha: 'abc123def4567890abcdef1234567890abcdef12',
    status: 'Passed',
    createdAt: '2026-07-10T10:00:00.000Z',
    jobs: [
      {
        id: '99999999-0000-0000-0000-000000000011',
        runId: '99999999-0000-0000-0000-000000000001',
        name: 'build',
        stage: 'build',
        runsOn: 'ogb-hosted',
        status: 'Passed',
        createdAt: '2026-07-10T10:00:05.000Z',
      },
      {
        id: '99999999-0000-0000-0000-000000000012',
        runId: '99999999-0000-0000-0000-000000000001',
        name: 'test',
        stage: 'test',
        runsOn: 'ogb-hosted',
        status: 'Passed',
        createdAt: '2026-07-10T10:00:20.000Z',
      },
    ],
  },
]

export const handlers = [
  http.get('/api/account/me', () => {
    return HttpResponse.json(mockUser)
  }),

  http.get('/api/repository', () => {
    return HttpResponse.json(mockRepos)
  }),

  http.get('/api/organization', () => {
    return HttpResponse.json(mockOrgs)
  }),

  http.get('/api/organization/by-slug/:slug', ({ params }) => {
    const org = mockOrgs.find(o => o.slug === params.slug)
    if (!org) {
      return new HttpResponse(null, { status: 404 })
    }
    return HttpResponse.json(org)
  }),

  http.get('/api/organization/:id/members', ({ params }) => {
    if (params.id === '44444444-4444-4444-4444-444444444444') {
      return HttpResponse.json(mockOrgMembers)
    }
    return new HttpResponse(null, { status: 403 })
  }),

  http.get('/api/organization/:id/invites', ({ params }) => {
    if (params.id === '44444444-4444-4444-4444-444444444444') {
      return HttpResponse.json(mockOrgInvites)
    }
    return new HttpResponse(null, { status: 403 })
  }),

  http.post('/api/organization/:id/members', () => {
    return new HttpResponse(JSON.stringify({ invited: true }), { status: 202 })
  }),

  http.get('/api/invite/:token', ({ params }) => {
    if (params.token === 'demo-token') {
      return HttpResponse.json({
        organizationName: 'Acme Corp',
        organizationSlug: 'acme-corp',
        email: 'person@example.com',
        role: 0,
        expiresAt: '2026-06-08T12:00:00Z',
        status: 0,
      })
    }
    return new HttpResponse(null, { status: 404 })
  }),

  http.get('/api/discovery/repositories', () => {
    return HttpResponse.json(mockRepos.filter(r => !r.isPrivate))
  }),

  http.get('/api/discovery/feed/recent', () => {
    return HttpResponse.json(mockRepos.filter(r => !r.isPrivate))
  }),

  http.get('/api/public/owners/:owner', ({ params }) => {
    const owner = String(params.owner)
    if (owner === 'acme-corp') {
      return HttpResponse.json({
        slug: 'acme-corp',
        name: 'Acme Corp',
        kind: 'organization',
        bio: 'Building things together.',
        repositories: mockRepos.filter(r => !r.isPrivate),
      })
    }
    return HttpResponse.json({
      slug: owner,
      name: owner,
      kind: 'user',
      repositories: mockRepos.filter(r => !r.isPrivate),
    })
  }),

  http.get('/api/repository/by-slug/:owner/:slug', ({ params }) => {
    const repo = mockRepos.find(r => r.slug === params.slug)
    if (!repo) {
      return new HttpResponse(null, { status: 404 })
    }
    return HttpResponse.json(repo)
  }),

  http.get('/api/repository/by-slug/:owner/:slug/discussions/:number', ({ request }) => {
    const url = new URL(request.url)
    if (!url.pathname.endsWith('/discussions/1')) {
      return new HttpResponse(null, { status: 404 })
    }
    const includeComments = url.searchParams.get('include') === 'comments'
    if (!includeComments) {
      const { comments: _comments, ...discussionWithoutComments } = mockDiscussionDetail
      return HttpResponse.json(discussionWithoutComments)
    }
    return HttpResponse.json(mockDiscussionDetail)
  }),

  http.get('/api/repository/by-slug/:owner/:slug/discussions/:number/comments', ({ request }) => {
    const url = new URL(request.url)
    if (!url.pathname.endsWith('/discussions/1/comments')) {
      return new HttpResponse(null, { status: 404 })
    }
    return HttpResponse.json(mockDiscussionDetail.comments)
  }),

  http.get('/api/repository/by-slug/:owner/:slug/content/refs', () => {
    return HttpResponse.json({
      branches: [{ name: 'main', commitSha: 'abc123' }],
      tags: [],
      defaultRef: 'main',
      isEmpty: false,
    })
  }),

  http.get('/api/repository-member/:repositoryId', ({ params }) => {
    if (params.repositoryId === '11111111-1111-1111-1111-111111111111') {
      return HttpResponse.json(mockMembers)
    }
    return HttpResponse.json([])
  }),

  http.get('/api/repository/:id/usage', () => {
    return HttpResponse.json({
      bytesUsed: 524288000,
      bytesLimit: 1073741824,
      fileSizeLimit: 52428800,
    })
  }),

  http.get('/api/repository/:id/pipelines', ({ params }) => {
    if (params.id === '11111111-1111-1111-1111-111111111111') {
      return HttpResponse.json(mockPipelineRuns)
    }
    return HttpResponse.json([])
  }),

  http.get('/api/pipeline/runs/:runId', ({ params }) => {
    const run = mockPipelineRuns.find(item => item.id === params.runId)
    if (!run) {
      return new HttpResponse(null, { status: 404 })
    }
    return HttpResponse.json(run)
  }),

  http.get('/api/pipeline/jobs/:jobId/logs', ({ params }) => {
    const logs = {
      '99999999-0000-0000-0000-000000000011': [
        {
          section: 'workspace',
          line: 'Workspace prepared at /tmp/opengitbase-agent/run-1/repo',
          timestamp: '2026-07-10T10:00:06.000Z',
        },
      ],
      '99999999-0000-0000-0000-000000000012': [
        {
          section: 'script',
          line: 'Running test suite...',
          timestamp: '2026-07-10T10:00:22.000Z',
        },
        {
          section: 'script',
          line: 'All tests passed.',
          timestamp: '2026-07-10T10:00:40.000Z',
        },
      ],
    } as Record<string, unknown[]>
    return HttpResponse.json(logs[String(params.jobId)] ?? [])
  }),

  http.get('/api/public-git-ssh-key', () => {
    return HttpResponse.json([
      {
        id: '55555555-5555-5555-5555-555555555555',
        name: 'Laptop',
        publicSSHKey: 'ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAI...',
        fingerprint: 'SHA256:abc123',
      },
    ])
  }),

  http.post('/api/signin/login', async () => {
    return HttpResponse.json('mock-token')
  }),

  http.post('/api/register/register', async () => {
    return HttpResponse.json('mock-token')
  }),

  http.post('/api/signin/signout', async () => {
    return HttpResponse.json('Signed out')
  }),

  http.get('/api/public/status', () => {
    return HttpResponse.json({
      overallStatus: 0,
      checkedAt: '2026-07-10T12:00:00Z',
      incident: null,
      openWindows: [],
      groups: [
        {
          group: 1,
          status: 0,
          instances: [{ instanceId: 'web-1', status: 0, lastCheckedAt: '2026-07-10T12:00:00Z', responseTimeMs: 42 }],
        },
        {
          group: 2,
          status: 0,
          instances: [{ instanceId: 'api-1', status: 0, lastCheckedAt: '2026-07-10T12:00:00Z', responseTimeMs: 18 }],
        },
        {
          group: 3,
          status: 1,
          instances: [{ instanceId: 'dispatcher-1', status: 1, lastCheckedAt: '2026-07-10T12:00:00Z', responseTimeMs: 2100, message: 'Slow response (2100ms)' }],
        },
        {
          group: 4,
          status: 0,
          instances: [{ instanceId: 'storage-1', status: 0, lastCheckedAt: '2026-07-10T12:00:00Z', lastSeenAt: '2026-07-10T11:59:30Z' }],
        },
        {
          group: 5,
          status: 0,
          instances: [
            { instanceId: 'postgres', status: 0, lastCheckedAt: '2026-07-10T12:00:00Z', responseTimeMs: 3 },
            { instanceId: 'redis', status: 0, lastCheckedAt: '2026-07-10T12:00:00Z', responseTimeMs: 2 },
          ],
        },
      ],
    })
  }),

  http.get('/api/public/status/history', () => {
    return HttpResponse.json({
      overall: [
        { date: '2026-07-09', uptimePercent: 99.5, healthyRatio: 0.98, degradedRatio: 0.02, unhealthyRatio: 0 },
        { date: '2026-07-10', uptimePercent: 97.2, healthyRatio: 0.94, degradedRatio: 0.04, unhealthyRatio: 0.02 },
      ],
      overallStateMix: [
        { date: '2026-07-09', uptimePercent: 99.5, healthyRatio: 0.98, degradedRatio: 0.02, unhealthyRatio: 0 },
        { date: '2026-07-10', uptimePercent: 97.2, healthyRatio: 0.94, degradedRatio: 0.04, unhealthyRatio: 0.02 },
      ],
      groups: [
        { group: 1, days: [{ date: '2026-07-10', uptimePercent: 100, healthyRatio: 1, degradedRatio: 0, unhealthyRatio: 0 }] },
        { group: 2, days: [{ date: '2026-07-10', uptimePercent: 100, healthyRatio: 1, degradedRatio: 0, unhealthyRatio: 0 }] },
        { group: 3, days: [{ date: '2026-07-10', uptimePercent: 90, healthyRatio: 0.8, degradedRatio: 0.2, unhealthyRatio: 0 }] },
        { group: 4, days: [{ date: '2026-07-10', uptimePercent: 100, healthyRatio: 1, degradedRatio: 0, unhealthyRatio: 0 }] },
        { group: 5, days: [{ date: '2026-07-10', uptimePercent: 100, healthyRatio: 1, degradedRatio: 0, unhealthyRatio: 0 }] },
      ],
    })
  }),

  http.get('/api/public/status/windows', () => {
    return HttpResponse.json([
      {
        id: 'window-open',
        scope: 0,
        group: 6,
        instanceId: null,
        displayName: 'Message bus',
        startedAt: '2026-07-19T10:00:00Z',
        endedAt: null,
        isOpen: true,
        isPartial: false,
        durationMinutes: 65,
        annotation: null,
      },
      {
        id: 'window-closed-annotated',
        scope: 0,
        group: 3,
        instanceId: null,
        displayName: 'Git',
        startedAt: '2026-07-18T08:00:00Z',
        endedAt: '2026-07-18T08:40:00Z',
        isOpen: false,
        isPartial: false,
        durationMinutes: 40,
        annotation: 'Scheduled failover drill.',
      },
      {
        id: 'window-partial',
        scope: 1,
        group: 3,
        instanceId: 'dispatcher-2',
        displayName: 'dispatcher-2',
        startedAt: '2026-07-17T22:00:00Z',
        endedAt: '2026-07-17T22:12:00Z',
        isOpen: false,
        isPartial: true,
        durationMinutes: 12,
        annotation: null,
      },
    ])
  }),

  http.get('/api/admin/status/incident', () => HttpResponse.json(null)),

  http.get('/api/admin/status/windows', () => HttpResponse.json([])),
  http.post('/api/admin/status/windows/:windowId/suppress', ({ params }) => {
    return HttpResponse.json({
      id: params.windowId,
      scope: 0,
      group: 6,
      instanceId: null,
      displayName: 'Message bus',
      startedAt: '2026-07-10T12:00:00Z',
      endedAt: null,
      isOpen: true,
      isPartial: false,
      durationMinutes: 45,
      suppressed: true,
      annotation: null,
    })
  }),
  http.post('/api/admin/status/windows/:windowId/unsuppress', ({ params }) => {
    return HttpResponse.json({
      id: params.windowId,
      scope: 0,
      group: 6,
      instanceId: null,
      displayName: 'Message bus',
      startedAt: '2026-07-10T12:00:00Z',
      endedAt: null,
      isOpen: true,
      isPartial: false,
      durationMinutes: 45,
      suppressed: false,
      annotation: null,
    })
  }),
  http.put('/api/admin/status/windows/:windowId/annotation', async ({ params, request }) => {
    const body = await request.json() as { annotation?: string | null }
    return HttpResponse.json({
      id: params.windowId,
      scope: 0,
      group: 6,
      instanceId: null,
      displayName: 'Message bus',
      startedAt: '2026-07-10T12:00:00Z',
      endedAt: null,
      isOpen: true,
      isPartial: false,
      durationMinutes: 45,
      suppressed: false,
      annotation: body?.annotation ?? null,
    })
  }),
]
