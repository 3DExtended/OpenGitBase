import { http, HttpResponse } from 'msw'

const mockUser = {
  username: 'demo-user',
  emailVerified: false,
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
    name: 'acme-corp',
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

  http.get('/api/profile/:owner', ({ params }) => {
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

  http.get('/api/repository/:id/usage', () => {
    return HttpResponse.json({
      bytesUsed: 524288000,
      bytesLimit: 1073741824,
      fileSizeLimit: 52428800,
    })
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
]
