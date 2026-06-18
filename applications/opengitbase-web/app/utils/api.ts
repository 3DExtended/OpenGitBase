export interface ApiResult<T> {
  data: T | null
  error: string | null
  status: number
}

export interface StorageNodeDto {
  id: string
  nodeId: string
  internalHost: string
  internalSshPort: number
  internalHttpPort: number
  freeBytesAvailable: number
  totalBytesAvailable: number
  lastHeartbeatAt?: string | null
  isHealthy: boolean
  registeredAt: string
  certificateThumbprint?: string
}

export interface StorageNodeEnrollmentDto {
  id: string
  nodeId: string
  createdAt: string
  expiresAt: string
  consumedAt?: string | null
}

export interface CreateStorageNodeEnrollmentResult {
  enrollmentId: string
  nodeId: string
  enrollmentToken: string
  expiresAt: string
}

export interface AccountDebugFeatures {
  emailVerification: boolean
}

export interface AccountMe {
  username: string
  emailVerified: boolean
  isAdmin: boolean
  debug?: AccountDebugFeatures
}

export interface DebugVerificationCode {
  code: string
  expiresAt: string
}

export interface Repository {
  id: string
  name: string
  slug: string
  ownerUserId: string
  ownerKind?: 'user' | 'organization'
  ownerId?: string
  ownerSlug?: string
  isPrivate: boolean
  physicalPath?: string
  updatedAt?: string
}

export interface Organization {
  id: string
  name: string
  slug?: string
}

export interface OrganizationMember {
  id: string
  organizationId: string
  userId: string
  username?: string
  role: number
}

export interface OrganizationInvite {
  id: string
  organizationId: string
  email: string
  role: number
  invitedByUserId: string
  createdAt: string
  expiresAt: string
  status: number
}

export interface OrganizationInvitePublic {
  organizationName: string
  organizationSlug: string
  email: string
  role: number
  expiresAt: string
  status: number
}

export interface PublicGitSshKey {
  id: string
  name: string
  publicSSHKey: string
  fingerprint?: string
  ownerUserId?: string
}

export interface RepositoryMember {
  id: string
  repositoryId: string
  userId: string
  username?: string
  role: number
}

export interface RepositoryUsage {
  bytesUsed: number
  bytesLimit: number
  fileSizeLimit: number
}

export interface OwnerProfile {
  slug: string
  name: string
  kind: 'user' | 'organization'
  bio?: string
  repositories: Repository[]
}

export interface DeleteAccountBlocker {
  type: 'repository' | 'organization'
  name: string
  slug: string
}

export interface DeleteAccountResult {
  success: boolean
  blockers?: DeleteAccountBlocker[]
}

function normalizeId(value: unknown): string {
  if (typeof value === 'string') {
    return value
  }
  if (value && typeof value === 'object' && 'value' in value) {
    return String((value as { value: unknown }).value)
  }
  return String(value ?? '')
}

function normalizeRepository(raw: Record<string, unknown>): Repository {
  return {
    id: normalizeId(raw.id),
    name: String(raw.name ?? ''),
    slug: String(raw.slug ?? ''),
    ownerUserId: normalizeId(raw.ownerUserId),
    ownerKind: raw.ownerKind as Repository['ownerKind'],
    ownerId: raw.ownerId ? normalizeId(raw.ownerId) : undefined,
    ownerSlug: raw.ownerSlug ? String(raw.ownerSlug) : undefined,
    isPrivate: Boolean(raw.isPrivate),
    physicalPath: raw.physicalPath ? String(raw.physicalPath) : undefined,
    updatedAt: raw.updatedAt ? String(raw.updatedAt) : undefined,
  }
}

function normalizeOrganization(raw: Record<string, unknown>): Organization {
  return {
    id: normalizeId(raw.id),
    name: String(raw.name ?? ''),
    slug: raw.slug ? String(raw.slug) : undefined,
  }
}

function normalizeOrganizationMember(raw: Record<string, unknown>): OrganizationMember {
  return {
    id: normalizeId(raw.id),
    organizationId: normalizeId(raw.organizationId),
    userId: normalizeId(raw.userId),
    username: raw.username ? String(raw.username) : undefined,
    role: Number(raw.role ?? 0),
  }
}

function normalizeOrganizationInvite(raw: Record<string, unknown>): OrganizationInvite {
  return {
    id: normalizeId(raw.id),
    organizationId: normalizeId(raw.organizationId),
    email: String(raw.email ?? ''),
    role: Number(raw.role ?? 0),
    invitedByUserId: normalizeId(raw.invitedByUserId),
    createdAt: String(raw.createdAt ?? ''),
    expiresAt: String(raw.expiresAt ?? ''),
    status: Number(raw.status ?? 0),
  }
}

function normalizeOrganizationInvitePublic(raw: Record<string, unknown>): OrganizationInvitePublic {
  return {
    organizationName: String(raw.organizationName ?? ''),
    organizationSlug: String(raw.organizationSlug ?? ''),
    email: String(raw.email ?? ''),
    role: Number(raw.role ?? 0),
    expiresAt: String(raw.expiresAt ?? ''),
    status: Number(raw.status ?? 0),
  }
}

function normalizeSshKey(raw: Record<string, unknown>): PublicGitSshKey {
  return {
    id: normalizeId(raw.id),
    name: String(raw.name ?? ''),
    publicSSHKey: String(raw.publicSSHKey ?? raw.publicSshKey ?? ''),
    fingerprint: raw.fingerprint ? String(raw.fingerprint) : undefined,
    ownerUserId: raw.ownerUserId ? normalizeId(raw.ownerUserId) : undefined,
  }
}

function normalizeMember(raw: Record<string, unknown>): RepositoryMember {
  return {
    id: normalizeId(raw.id),
    repositoryId: normalizeId(raw.repositoryId),
    userId: normalizeId(raw.userId),
    username: raw.username ? String(raw.username) : undefined,
    role: Number(raw.role ?? 0),
  }
}

async function parseError(response: Response): Promise<string> {
  try {
    const body = await response.json() as {
      error?: string
      message?: string
      title?: string
      errors?: Record<string, string[]>
    }
    if (body.errors) {
      const messages = Object.values(body.errors).flat()
      if (messages.length > 0) {
        return messages.join(' ')
      }
    }
    return body.error ?? body.message ?? body.title ?? response.statusText
  }
  catch {
    try {
      const text = await response.text()
      return text || response.statusText
    }
    catch {
      return response.statusText
    }
  }
}

export function createApi(baseUrl: string) {
  async function request<T>(
    path: string,
    options: RequestInit = {},
  ): Promise<ApiResult<T>> {
    const headers = new Headers(options.headers)
    if (options.body && !headers.has('Content-Type')) {
      headers.set('Content-Type', 'application/json')
    }

    try {
      const response = await fetch(`${baseUrl}${path}`, {
        ...options,
        headers,
        credentials: 'include',
        cache: 'no-store',
      })

      if (response.status === 204) {
        return { data: null, error: null, status: response.status }
      }

      const contentType = response.headers.get('content-type') ?? ''
      const isJson = contentType.includes('application/json')

      if (!response.ok) {
        const error = isJson
          ? await parseError(response)
          : (await response.text()) || response.statusText
        return { data: null, error, status: response.status }
      }

      if (!isJson) {
        const text = await response.text()
        // HTML means the reverse proxy served the SPA instead of the API.
        if (text.startsWith('<!') || text.startsWith('<html')) {
          return { data: null, error: 'Unexpected HTML response', status: response.status }
        }
        return { data: text as T, error: null, status: response.status }
      }

      const data = await response.json() as T
      return { data, error: null, status: response.status }
    }
    catch (error) {
      return {
        data: null,
        error: error instanceof Error ? error.message : 'Network error',
        status: 0,
      }
    }
  }

  return {
    auth: {
      login: (body: { username: string, password: string }) =>
        request<string>('/signin/login', { method: 'POST', body: JSON.stringify(body) }),

      register: (body: { username: string, email: string, password: string }) =>
        request<string>('/register/register', { method: 'POST', body: JSON.stringify(body) }),

      signOut: () =>
        request<string>('/signin/signout', { method: 'POST' }),

      requestPasswordReset: (body: { username: string, email: string }) =>
        request<string>('/signin/requestresetpassword', { method: 'POST', body: JSON.stringify(body) }),

      resetPassword: (body: {
        username: string
        email: string
        resetCode: string
        newPassword: string
      }) =>
        request<string>('/signin/resetpassword', { method: 'POST', body: JSON.stringify(body) }),
    },

    account: {
      me: async () => {
        const result = await request<{
          username: string
          emailVerified: boolean
          isAdmin: boolean
          debug?: { emailVerification: boolean }
        }>('/account/me')
        if (!result.data) {
          return result as ApiResult<AccountMe>
        }
        return {
          ...result,
          data: {
            username: result.data.username,
            emailVerified: result.data.emailVerified,
            isAdmin: result.data.isAdmin,
            debug: result.data.debug?.emailVerification
              ? { emailVerification: true }
              : undefined,
          },
        }
      },

      changePassword: (body: { currentPassword: string, newPassword: string }) =>
        request<string>('/account/change-password', { method: 'POST', body: JSON.stringify(body) }),

      verifyEmail: (body: { username: string, verificationToken: string }) =>
        request<string>('/account/verify-email', { method: 'POST', body: JSON.stringify(body) }),

      resendVerification: () =>
        request<string>('/account/resend-verification', { method: 'POST' }),

      debugVerifyEmail: () =>
        request<string>('/account/debug/verify-email', { method: 'POST' }),

      debugVerificationCode: () =>
        request<DebugVerificationCode>('/account/debug/verification-code', { method: 'POST' }),

      deleteAccount: (body: { password: string }) =>
        request<DeleteAccountResult>('/account/delete', { method: 'POST', body: JSON.stringify(body) }),
    },

    repositories: {
      list: async () => {
        const result = await request<Record<string, unknown>[]>('/repository')
        return {
          ...result,
          data: result.data?.map(normalizeRepository) ?? null,
        }
      },

      get: async (id: string) => {
        const result = await request<Record<string, unknown>>(`/repository/${id}`)
        return {
          ...result,
          data: result.data ? normalizeRepository(result.data) : null,
        }
      },

      getBySlug: async (owner: string, slug: string) => {
        const result = await request<Record<string, unknown>>(`/repository/by-slug/${owner}/${slug}`)
        return {
          ...result,
          data: result.data ? normalizeRepository(result.data) : null,
        }
      },

      create: (slug: string, body: { repositoryName: string, isPrivate: boolean }) =>
        request<string>(`/repository/${encodeURIComponent(slug)}`, {
          method: 'POST',
          body: JSON.stringify(body),
        }),

      update: (id: string, body: { name: string, isPrivate: boolean }) =>
        request<null>(`/repository/${id}`, { method: 'PUT', body: JSON.stringify(body) }),

      delete: (id: string) =>
        request<null>(`/repository/${id}`, { method: 'DELETE' }),

      usage: async (id: string) => {
        const result = await request<Record<string, unknown>>(`/repository/${id}/usage`)
        if (!result.data) {
          return { ...result, data: null } satisfies ApiResult<RepositoryUsage>
        }
        return {
          ...result,
          data: {
            bytesUsed: Number(result.data.bytesUsed ?? 0),
            bytesLimit: Number(result.data.bytesLimit ?? 1_073_741_824),
            fileSizeLimit: Number(result.data.fileSizeLimit ?? 52_428_800),
          },
        }
      },
    },

    repositoryMembers: {
      list: async (repositoryId: string) => {
        const result = await request<Record<string, unknown>[]>(`/repository-member/${repositoryId}`)
        return {
          ...result,
          data: result.data?.map(normalizeMember) ?? null,
        }
      },

      create: (body: { modelToCreate: { repositoryId: string, userId: string, role: number } }) =>
        request<string>('/repository-member', { method: 'POST', body: JSON.stringify(body) }),

      update: (id: string, body: { updatedModel: { id: string, repositoryId: string, userId: string, role: number } }) =>
        request<null>(`/repository-member/${id}`, { method: 'PUT', body: JSON.stringify(body) }),

      delete: (id: string) =>
        request<null>(`/repository-member/${id}`, { method: 'DELETE' }),
    },

    organizations: {
      list: async () => {
        const result = await request<Record<string, unknown>[]>('/organization')
        return {
          ...result,
          data: result.data?.map(normalizeOrganization) ?? null,
        }
      },

      get: async (id: string) => {
        const result = await request<Record<string, unknown>>(`/organization/${id}`)
        return {
          ...result,
          data: result.data ? normalizeOrganization(result.data) : null,
        }
      },

      getBySlug: async (slug: string) => {
        const result = await request<Record<string, unknown>>(`/organization/by-slug/${encodeURIComponent(slug)}`)
        return {
          ...result,
          data: result.data ? normalizeOrganization(result.data) : null,
        }
      },

      members: {
        list: async (organizationId: string) => {
          const result = await request<Record<string, unknown>[]>(`/organization/${organizationId}/members`)
          return {
            ...result,
            data: result.data?.map(normalizeOrganizationMember) ?? null,
          }
        },

        updateRole: (organizationId: string, userId: string, body: { role: number }) =>
          request<null>(`/organization/${organizationId}/members/${userId}`, {
            method: 'PUT',
            body: JSON.stringify(body),
          }),

        remove: (organizationId: string, userId: string) =>
          request<null>(`/organization/${organizationId}/members/${userId}`, { method: 'DELETE' }),

        add: (organizationId: string, body: { identifier: string, role: number }) =>
          request<null>(`/organization/${organizationId}/members`, {
            method: 'POST',
            body: JSON.stringify(body),
          }),
      },

      invites: {
        list: async (organizationId: string) => {
          const result = await request<Record<string, unknown>[]>(`/organization/${organizationId}/invites`)
          return {
            ...result,
            data: result.data?.map(normalizeOrganizationInvite) ?? null,
          }
        },

        resend: (organizationId: string, inviteId: string) =>
          request<null>(`/organization/${organizationId}/invites/${inviteId}/resend`, { method: 'POST' }),

        revoke: (organizationId: string, inviteId: string) =>
          request<null>(`/organization/${organizationId}/invites/${inviteId}`, { method: 'DELETE' }),
      },

      create: (body: { modelToCreate: { name: string, slug: string } }) =>
        request<string>('/organization', { method: 'POST', body: JSON.stringify(body) }),

      update: (id: string, body: { updatedModel: { name: string } }) =>
        request<null>(`/organization/${id}`, { method: 'PUT', body: JSON.stringify(body) }),

      delete: (id: string) =>
        request<null>(`/organization/${id}`, { method: 'DELETE' }),
    },

    sshKeys: {
      list: async () => {
        const result = await request<Record<string, unknown>[]>('/public-git-ssh-key')
        return {
          ...result,
          data: result.data?.map(normalizeSshKey) ?? null,
        }
      },

      create: (body: { modelToCreate: { name: string, publicSSHKey: string, fingerprint?: string } }) =>
        request<string>('/public-git-ssh-key', { method: 'POST', body: JSON.stringify(body) }),

      delete: (id: string) =>
        request<null>(`/public-git-ssh-key/${id}`, { method: 'DELETE' }),
    },

    discovery: {
      listPublic: async (params?: { q?: string }) => {
        const query = params?.q ? `?q=${encodeURIComponent(params.q)}` : ''
        const result = await request<Record<string, unknown>[]>(`/public/repositories${query}`)
        return {
          ...result,
          data: result.data?.map(normalizeRepository) ?? null,
        }
      },

      recentFeed: async () => {
        const result = await request<Record<string, unknown>[]>('/public/repositories/recent')
        return {
          ...result,
          data: result.data?.map(normalizeRepository) ?? null,
        }
      },

      getProfile: async (owner: string) => {
        const result = await request<Record<string, unknown>>(`/public/owners/${encodeURIComponent(owner)}`)
        if (!result.data) {
          return { ...result, data: null } satisfies ApiResult<OwnerProfile>
        }
        const repos = Array.isArray(result.data.repositories)
          ? (result.data.repositories as Record<string, unknown>[]).map(normalizeRepository)
          : []
        return {
          ...result,
          data: {
            slug: String(result.data.slug ?? owner),
            name: String(result.data.name ?? owner),
            kind: (result.data.kind as OwnerProfile['kind']) ?? 'user',
            bio: result.data.bio ? String(result.data.bio) : undefined,
            repositories: repos,
          },
        }
      },
    },

    invite: {
      getByToken: async (token: string) => {
        const result = await request<Record<string, unknown>>(`/invite/${encodeURIComponent(token)}`)
        return {
          ...result,
          data: result.data ? normalizeOrganizationInvitePublic(result.data) : null,
        }
      },

      accept: (token: string) =>
        request<null>(`/invite/${encodeURIComponent(token)}/accept`, { method: 'POST' }),

      decline: (token: string) =>
        request<null>(`/invite/${encodeURIComponent(token)}/decline`, { method: 'POST' }),
    },

    admin: {
      storageNodes: {
        list: () => request<StorageNodeDto[]>('/admin/storage-nodes'),
      },
      storageEnrollments: {
        list: () => request<StorageNodeEnrollmentDto[]>('/admin/storage-enrollments'),
        create: (body: { nodeId: string, expiresInHours?: number }) =>
          request<CreateStorageNodeEnrollmentResult>('/admin/storage-enrollments', {
            method: 'POST',
            body: JSON.stringify(body),
          }),
      },
      fleet: {
        getDispatcherSshPublicKey: () => request<{ publicKey: string }>('/admin/fleet/dispatcher-ssh-public-key'),
        generateDispatcherSshKeys: () =>
          request<{ dispatcherSshPublicKey: string, fleetBootstrapToken: string }>('/admin/fleet/dispatcher-ssh-keys/generate', {
            method: 'POST',
          }),
      },
    },
  }
}

export type ApiClient = ReturnType<typeof createApi>
