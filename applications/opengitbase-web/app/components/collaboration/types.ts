import type { CommentAnchorInput, RepositoryMember } from '~/utils/api'

export interface CollaborationAuthor {
  userId: string
  username?: string | null
}

export interface CollaborationThreadReply {
  id: string
  author: CollaborationAuthor
  bodyMarkdown: string
  createdAt: string
  anchor?: CommentAnchorInput | null
}

export interface CollaborationThread {
  id: string
  author: CollaborationAuthor
  bodyMarkdown: string
  createdAt: string
  isResolved: boolean
  isOutdated?: boolean
  replyCount: number
  orphanedFromDeletedRoot?: boolean
  replies: CollaborationThreadReply[]
  anchor?: CommentAnchorInput | null
}

export interface CollaborationThreadProps {
  thread: CollaborationThread
  owner: string
  repoSlug: string
  memberLabel: (userId: string, preferredUsername?: string | null) => string
  members?: RepositoryMember[]
  canResolve: boolean
  canReply: boolean
  defaultRef?: string | null
  resolvedLabel?: string
  outdatedLabel?: string
  replyCountLabel?: (count: number) => string
}
