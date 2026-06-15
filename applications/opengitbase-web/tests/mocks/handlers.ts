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
