import type { DiscussionComment, Repository, RepositoryMember } from '~/utils/api'

export const RepositoryRole = {
  None: 0,
  Reader: 1,
  Writer: 2,
  Admin: 3,
  Owner: 4,
} as const

const REPOSITORY_ROLE_BY_NAME: Record<string, number> = {
  None: RepositoryRole.None,
  Reader: RepositoryRole.Reader,
  Writer: RepositoryRole.Writer,
  Admin: RepositoryRole.Admin,
  Owner: RepositoryRole.Owner,
}

export function parseRepositoryRole(raw: unknown): number {
  if (typeof raw === 'string') {
    const named = REPOSITORY_ROLE_BY_NAME[raw]
    if (named !== undefined) {
      return named
    }
  }
  const numeric = Number(raw ?? 0)
  return Number.isFinite(numeric) ? numeric : RepositoryRole.None
}

export function normalizeRepositoryMemberRole(raw: unknown): number {
  return parseRepositoryRole(raw)
}

export function usernamesMatch(
  left?: string | null,
  right?: string | null,
): boolean {
  if (!left || !right) {
    return false
  }
  return left.toLowerCase() === right.toLowerCase()
}

export function findRepositoryMember(
  members: RepositoryMember[],
  username?: string | null,
): RepositoryMember | undefined {
  if (!username) {
    return undefined
  }
  return members.find(member => usernamesMatch(member.username, username))
}

export function isPersonalRepoOwner(
  username: string | null | undefined,
  repo: Pick<Repository, 'ownerSlug' | 'ownerKind'> | null | undefined,
): boolean {
  if (!username || !repo?.ownerSlug) {
    return false
  }
  if (repo.ownerKind === 'organization') {
    return false
  }
  return usernamesMatch(username, repo.ownerSlug)
}

export function resolveEffectiveRepositoryRole(input: {
  viewerEffectiveRole?: number
  username?: string | null
  members?: RepositoryMember[]
  repo?: Pick<Repository, 'ownerSlug' | 'ownerKind'> | null
}): number {
  const fromApi = input.viewerEffectiveRole ?? RepositoryRole.None
  if (fromApi >= RepositoryRole.Reader) {
    return fromApi
  }

  const member = findRepositoryMember(input.members ?? [], input.username)
  if (member && member.role >= RepositoryRole.Reader) {
    return member.role
  }

  if (isPersonalRepoOwner(input.username, input.repo)) {
    return RepositoryRole.Owner
  }

  return RepositoryRole.None
}

export function canResolveDiscussionSubThread(input: {
  comment: DiscussionComment
  userId?: string | null
  username?: string | null
  effectiveRole: number
}): boolean {
  if (!input.username) {
    return false
  }

  const isAuthor =
    (input.userId != null && input.userId === input.comment.authorUserId)
    || usernamesMatch(input.username, input.comment.authorUsername)

  const isWriterPlus = input.effectiveRole >= RepositoryRole.Writer

  return isAuthor || isWriterPlus
}
