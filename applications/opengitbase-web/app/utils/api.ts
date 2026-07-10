import { normalizeCommentId } from './discussionCommentHash'
import { normalizeRepositoryMemberRole } from './discussionPermissions'

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

export interface AdminStorageNodeReplicationSummaryDto {
  storageNodeId: string
  nodeId: string
  primaryRepositoryCount: number
  replicaRepositoryCount: number
  isSpare: boolean
}

export interface AdminRepositoryReplicationSummaryDto {
  repositoryId: string
  name: string
  ownerSlug: string
  replicationState: string
  replicaCount: number
  primaryNodeId: string
  primaryWatermark: number
  maxWatermarkLag: number
  writeQuorumAvailable: boolean
  replicationEpoch: number
  oldestLastSyncedAt?: string | null
}

export interface AdminRepositoryReplicationListResponse {
  items: AdminRepositoryReplicationSummaryDto[]
  totalCount: number
  page: number
  pageSize: number
}

export interface AdminRepositoryReplicaStatusDto {
  storageNodeId: string
  nodeId: string
  role: string
  appliedWatermark: number
  artifactWatermark?: number | null
  isInSync: boolean
  lastSyncedAt?: string | null
}

export interface AdminRepositoryReplicationDetailDto {
  repositoryId: string
  name: string
  slug: string
  ownerSlug: string
  replicationState: string
  primaryWatermark: number
  replicationEpoch: number
  writeQuorumAvailable: boolean
  replicas: AdminRepositoryReplicaStatusDto[]
}

export interface AccountDebugFeatures {
  emailVerification: boolean
}

export interface AccountMe {
  userId: string
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

export interface GitConfig {
  gitBaseUrl: string
  sshEnabled: boolean
}

export interface GitAccessToken {
  id: string
  name: string
  scope: string
  createdAt: string
  expiresAt?: string | null
  revokedAt?: string | null
  ownerUserId?: string
}

export interface CreateGitAccessTokenResult {
  id: string
  token: string
  metadata: GitAccessToken
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

export interface RepositoryContentRef {
  name: string
  commitSha: string
}

export interface RepositoryReplicationLag {
  behind: boolean
  message?: string | null
}

export interface RepositoryContentRefs {
  branches: RepositoryContentRef[]
  tags: RepositoryContentRef[]
  defaultRef: string | null
  isEmpty: boolean
  replicationLag: RepositoryReplicationLag | null
}

export interface RepositoryContentEntry {
  name: string
  path: string
  type: string
  size: number | null
}

export interface RepositoryContentTree {
  ref: string
  path: string
  entries: RepositoryContentEntry[]
  replicationLag: RepositoryReplicationLag | null
}

export interface RepositoryContentBlob {
  ref: string
  path: string
  size: number
  isBinary: boolean
  isTooLarge: boolean
  previewKind: string
  textContent: string | null
  contentBase64: string | null
  replicationLag: RepositoryReplicationLag | null
}

export interface RepositoryContentReadme {
  ref: string
  fileName: string
  markdownSource: string
  replicationLag: RepositoryReplicationLag | null
}

export type DiscussionStatus = 'Open' | 'Engaged' | 'Resolved' | 'Dismissed'

export interface RepositoryTag {
  id: string
  repositoryId: string
  name: string
  color?: string | null
  createdAt: string
}

export interface Discussion {
  id: string
  repositoryId: string
  number: number
  title: string
  body?: string | null
  status: DiscussionStatus
  hasEverBeenEngaged: boolean
  creatorUserId: string
  creatorUsername?: string | null
  assigneeUserId?: string | null
  assigneeUsername?: string | null
  createdAt: string
  updatedAt: string
  tags: RepositoryTag[]
  comments?: DiscussionComment[]
  viewerEffectiveRole?: string | null
}

export interface AnchorResolution {
  kind: string
  filePath?: string | null
  line?: number | null
}

export interface CommentAnchor {
  ref: string
  commitSha: string
  filePath: string
  line: number
  endLine?: number | null
  resolution?: AnchorResolution | null
}

export interface DiscussionComment {
  id: string
  discussionId: string
  authorUserId: string
  authorUsername?: string | null
  bodyMarkdown: string
  createdAt: string
  updatedAt: string
  editedAt?: string | null
  deletedAt?: string | null
  deletedByUserId?: string | null
  isDeleted: boolean
  parentCommentId?: string | null
  isResolved: boolean
  resolvedAt?: string | null
  resolvedByUserId?: string | null
  replyCount: number
  lastReplyAt?: string | null
  orphanedFromDeletedRoot: boolean
  replies: DiscussionComment[]
  anchor?: CommentAnchor | null
}

export type MergeRequestStatus = 'Draft' | 'Open' | 'Approved' | 'Merged' | 'Closed'
export type MergeRequestLinkType = 'closes' | 'related' | 'implements'
export type DiffSide = 'new' | 'old'

export interface MergeRequest {
  id: string
  repositoryId: string
  number: number
  title: string
  body: string | null
  status: MergeRequestStatus
  isDraft: boolean
  creatorUserId: string
  creatorUsername?: string | null
  sourceRef: string
  targetRef: string
  sourceHeadSha: string
  targetBaseSha: string
  createdAt: string
  updatedAt: string
}

export interface MergeRequestCommentAnchor {
  headCommitSha: string
  filePath: string
  lineNumber: number
  diffSide: DiffSide
}

export interface MergeRequestComment {
  id: string
  mergeRequestId: string
  authorUserId: string
  authorUsername?: string | null
  bodyMarkdown: string
  createdAt: string
  updatedAt: string
  editedAt?: string | null
  deletedAt?: string | null
  isDeleted: boolean
  parentCommentId?: string | null
  isResolved: boolean
  isOutdated: boolean
  replyCount: number
  replies: MergeRequestComment[]
  anchor?: MergeRequestCommentAnchor | null
}

export interface MergeRequestDiffLine {
  oldLineNumber: number | null
  newLineNumber: number | null
  type: 'context' | 'add' | 'remove'
  content: string
}

export interface MergeRequestDiffHunk {
  header: string
  lines: MergeRequestDiffLine[]
}

export interface MergeRequestDiffFile {
  filePath: string
  oldPath?: string | null
  changeType: string
  hunks: MergeRequestDiffHunk[]
}

export interface MergeRequestChanges {
  files: MergeRequestDiffFile[]
}

export interface MergeRequestCommit {
  sha: string
  shortSha: string
  message: string
  authorName: string
  authoredAt: string
}

export interface RepositoryCommitParent {
  sha: string
  shortSha: string
}

export interface RepositoryCommitStats {
  filesChanged: number
  insertions: number
  deletions: number
}

export interface RepositoryCommitRootFile {
  path: string
  changeType: string
}

export interface RepositoryCommit {
  sha: string
  shortSha: string
  message: string
  authorName: string
  authoredAt: string
  parents: RepositoryCommitParent[]
  stats: RepositoryCommitStats | null
  kind: 'diff' | 'root'
  diffFiles: MergeRequestDiffFile[]
  rootFiles: RepositoryCommitRootFile[]
  replicationLag: RepositoryReplicationLag | null
}

export interface MergeRequestDiscussionLink {
  discussionNumber: number
  relationshipType: MergeRequestLinkType
  discussionTitle?: string | null
  discussionStatus?: string | null
}

export interface DiscussionLinkedMergeRequest {
  number: number
  title: string
  status: MergeRequestStatus
  relationshipType: MergeRequestLinkType
}

export interface BranchAheadSummary {
  aheadCount: number
  defaultRef: string | null
  hasActiveMergeRequest: boolean
}

export interface ProtectedBranchRule {
  id: string
  pattern: string
  blockDirectPush: boolean
  allowedPushRoles: string[]
  allowedPushUserIds: string[]
  requiredApprovals: number
  mergeRole: string
  forcePushPolicy: string
  dismissApprovalsOnPush: boolean
  lockedMergeStrategy?: string | null
  pushRules: {
    maxFileSizeBytes?: number | null
    forbiddenPathGlobs?: string[] | null
    commitMessageRegex?: string | null
    requireDcoSignoff?: boolean
  }
}

export type NotificationEventType =
  | 'NewComment'
  | 'Mention'
  | 'AssigneeChanged'
  | 'Resolved'
  | 'Dismissed'
  | 'Reopened'
  | 'SubThreadResolved'
  | 'MergeRequestComment'
  | 'MergeRequestMention'
  | 'MergeRequestApproved'
  | 'MergeRequestApprovalDismissed'
  | 'MergeRequestMerged'
  | 'MergeRequestClosed'
  | 'MergeRequestReviewThreadResolved'

export interface Notification {
  id: string
  userId: string
  discussionId?: string | null
  mergeRequestId?: string | null
  repositoryId?: string | null
  discussionNumber?: number | null
  mergeRequestNumber?: number | null
  commentId?: string | null
  ownerSlug: string
  repositorySlug: string
  eventType: NotificationEventType
  message: string
  actorUserId?: string | null
  createdAt: string
  readAt?: string | null
  isRead: boolean
}

export interface BlockedUser {
  userId: string
  username?: string | null
  blockedByUserId: string
  blockedAt: string
  reason?: string | null
}

export interface CommentAnchorInput {
  ref: string
  commitSha: string
  filePath: string
  line: number
  endLine?: number | null
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

function normalizeGitAccessToken(raw: Record<string, unknown>): GitAccessToken {
  return {
    id: normalizeId(raw.id),
    name: String(raw.name ?? ''),
    scope: String(raw.scope ?? ''),
    createdAt: String(raw.createdAt ?? ''),
    expiresAt: raw.expiresAt ? String(raw.expiresAt) : null,
    revokedAt: raw.revokedAt ? String(raw.revokedAt) : null,
    ownerUserId: raw.ownerUserId ? normalizeId(raw.ownerUserId) : undefined,
  }
}

function normalizeMember(raw: Record<string, unknown>): RepositoryMember {
  return {
    id: normalizeId(raw.id),
    repositoryId: normalizeId(raw.repositoryId),
    userId: normalizeId(raw.userId),
    username: raw.username ? String(raw.username) : undefined,
    role: normalizeRepositoryMemberRole(raw.role),
  }
}

function normalizeContentRef(raw: Record<string, unknown>): RepositoryContentRef {
  return {
    name: String(raw.name ?? ''),
    commitSha: String(raw.commitSha ?? ''),
  }
}

function normalizeReplicationLag(raw: unknown): RepositoryReplicationLag | null {
  if (!raw || typeof raw !== 'object') {
    return null
  }
  const record = raw as Record<string, unknown>
  return {
    behind: Boolean(record.behind),
    message: record.message ? String(record.message) : null,
  }
}

function normalizeContentEntry(raw: Record<string, unknown>): RepositoryContentEntry {
  return {
    name: String(raw.name ?? ''),
    path: String(raw.path ?? ''),
    type: String(raw.type ?? ''),
    size: raw.size == null ? null : Number(raw.size),
  }
}

function normalizeContentRefs(raw: Record<string, unknown>): RepositoryContentRefs {
  const branches = Array.isArray(raw.branches)
    ? (raw.branches as Record<string, unknown>[]).map(normalizeContentRef)
    : []
  const tags = Array.isArray(raw.tags)
    ? (raw.tags as Record<string, unknown>[]).map(normalizeContentRef)
    : []
  return {
    branches,
    tags,
    defaultRef: raw.defaultRef ? String(raw.defaultRef) : null,
    isEmpty: Boolean(raw.isEmpty),
    replicationLag: normalizeReplicationLag(raw.replicationLag),
  }
}

function normalizeContentTree(raw: Record<string, unknown>): RepositoryContentTree {
  const entries = Array.isArray(raw.entries)
    ? (raw.entries as Record<string, unknown>[]).map(normalizeContentEntry)
    : []
  return {
    ref: String(raw.ref ?? ''),
    path: String(raw.path ?? ''),
    entries,
    replicationLag: normalizeReplicationLag(raw.replicationLag),
  }
}

function normalizeContentBlob(raw: Record<string, unknown>): RepositoryContentBlob {
  return {
    ref: String(raw.ref ?? ''),
    path: String(raw.path ?? ''),
    size: Number(raw.size ?? 0),
    isBinary: Boolean(raw.isBinary),
    isTooLarge: Boolean(raw.isTooLarge),
    previewKind: String(raw.previewKind ?? 'text'),
    textContent: raw.textContent == null ? null : String(raw.textContent),
    contentBase64: raw.contentBase64 == null ? null : String(raw.contentBase64),
    replicationLag: normalizeReplicationLag(raw.replicationLag),
  }
}

function normalizeContentReadme(raw: Record<string, unknown>): RepositoryContentReadme {
  return {
    ref: String(raw.ref ?? ''),
    fileName: String(raw.fileName ?? ''),
    markdownSource: String(raw.markdownSource ?? ''),
    replicationLag: normalizeReplicationLag(raw.replicationLag),
  }
}

const DISCUSSION_STATUS_MAP: Record<number, DiscussionStatus> = {
  0: 'Open',
  1: 'Engaged',
  2: 'Resolved',
  3: 'Dismissed',
}

function normalizeDiscussionStatus(raw: unknown): DiscussionStatus {
  if (typeof raw === 'string' && ['Open', 'Engaged', 'Resolved', 'Dismissed'].includes(raw)) {
    return raw as DiscussionStatus
  }
  return DISCUSSION_STATUS_MAP[Number(raw)] ?? 'Open'
}

function normalizeRepositoryTag(raw: Record<string, unknown>): RepositoryTag {
  return {
    id: normalizeId(raw.id),
    repositoryId: normalizeId(raw.repositoryId),
    name: String(raw.name ?? ''),
    color: raw.color ? String(raw.color) : null,
    createdAt: String(raw.createdAt ?? ''),
  }
}

function normalizeDiscussion(raw: Record<string, unknown>): Discussion {
  const tags = Array.isArray(raw.tags)
    ? (raw.tags as Record<string, unknown>[]).map(normalizeRepositoryTag)
    : []
  const comments = Array.isArray(raw.comments)
    ? (raw.comments as Record<string, unknown>[]).map(normalizeDiscussionComment)
    : undefined
  return {
    id: normalizeId(raw.id),
    repositoryId: normalizeId(raw.repositoryId),
    number: Number(raw.number ?? 0),
    title: String(raw.title ?? ''),
    body: raw.body == null ? null : String(raw.body),
    status: normalizeDiscussionStatus(raw.status),
    hasEverBeenEngaged: Boolean(raw.hasEverBeenEngaged),
    creatorUserId: normalizeId(raw.creatorUserId),
    creatorUsername: raw.creatorUsername ? String(raw.creatorUsername) : null,
    assigneeUserId: raw.assigneeUserId ? normalizeId(raw.assigneeUserId) : null,
    assigneeUsername: raw.assigneeUsername ? String(raw.assigneeUsername) : null,
    createdAt: String(raw.createdAt ?? ''),
    updatedAt: String(raw.updatedAt ?? ''),
    tags,
    comments,
    viewerEffectiveRole: raw.viewerEffectiveRole ? String(raw.viewerEffectiveRole) : null,
  }
}

function normalizeAnchorResolution(raw: unknown): AnchorResolution | null {
  if (!raw || typeof raw !== 'object') {
    return null
  }
  const record = raw as Record<string, unknown>
  return {
    kind: String(record.kind ?? 'located'),
    filePath: record.filePath ? String(record.filePath) : null,
    line: record.line == null ? null : Number(record.line),
  }
}

function normalizeCommentAnchor(raw: Record<string, unknown>): CommentAnchor {
  return {
    ref: String(raw.ref ?? ''),
    commitSha: String(raw.commitSha ?? ''),
    filePath: String(raw.filePath ?? ''),
    line: Number(raw.line ?? 0),
    endLine: raw.endLine == null ? null : Number(raw.endLine),
    resolution: normalizeAnchorResolution(raw.resolution),
  }
}

function normalizeDiscussionComment(raw: Record<string, unknown>): DiscussionComment {
  const replies = Array.isArray(raw.replies)
    ? raw.replies.map(item => normalizeDiscussionComment(item as Record<string, unknown>))
    : []

  return {
    id: normalizeId(raw.id),
    discussionId: normalizeId(raw.discussionId),
    authorUserId: normalizeId(raw.authorUserId),
    authorUsername: raw.authorUsername ? String(raw.authorUsername) : null,
    bodyMarkdown: String(raw.bodyMarkdown ?? ''),
    createdAt: String(raw.createdAt ?? ''),
    updatedAt: String(raw.updatedAt ?? ''),
    editedAt: raw.editedAt ? String(raw.editedAt) : null,
    deletedAt: raw.deletedAt ? String(raw.deletedAt) : null,
    deletedByUserId: raw.deletedByUserId ? normalizeId(raw.deletedByUserId) : null,
    isDeleted: Boolean(raw.isDeleted),
    parentCommentId: raw.parentCommentId ? normalizeId(raw.parentCommentId) : null,
    isResolved: Boolean(raw.isResolved),
    resolvedAt: raw.resolvedAt ? String(raw.resolvedAt) : null,
    resolvedByUserId: raw.resolvedByUserId ? normalizeId(raw.resolvedByUserId) : null,
    replyCount: Number(raw.replyCount ?? replies.length),
    lastReplyAt: raw.lastReplyAt ? String(raw.lastReplyAt) : null,
    orphanedFromDeletedRoot: Boolean(raw.orphanedFromDeletedRoot),
    replies,
    anchor: raw.anchor && typeof raw.anchor === 'object'
      ? normalizeCommentAnchor(raw.anchor as Record<string, unknown>)
      : null,
  }
}

const MERGE_REQUEST_STATUS_MAP: Record<number, MergeRequestStatus> = {
  0: 'Draft',
  1: 'Open',
  2: 'Approved',
  3: 'Merged',
  4: 'Closed',
}

function normalizeMergeRequestStatus(raw: unknown): MergeRequestStatus {
  if (typeof raw === 'string' && ['Draft', 'Open', 'Approved', 'Merged', 'Closed'].includes(raw)) {
    return raw as MergeRequestStatus
  }
  return MERGE_REQUEST_STATUS_MAP[Number(raw)] ?? 'Open'
}

function normalizeMergeRequest(raw: Record<string, unknown>): MergeRequest {
  return {
    id: normalizeId(raw.id),
    repositoryId: normalizeId(raw.repositoryId),
    number: Number(raw.number ?? 0),
    title: String(raw.title ?? raw.name ?? ''),
    body: raw.body == null ? null : String(raw.body),
    status: normalizeMergeRequestStatus(raw.status),
    isDraft: Boolean(raw.isDraft ?? (normalizeMergeRequestStatus(raw.status) === 'Draft')),
    creatorUserId: normalizeId(raw.creatorUserId ?? raw.authorUserId),
    creatorUsername: raw.creatorUsername ? String(raw.creatorUsername) : null,
    sourceRef: String(raw.sourceRef ?? ''),
    targetRef: String(raw.targetRef ?? ''),
    sourceHeadSha: String(raw.sourceHeadSha ?? ''),
    targetBaseSha: String(raw.targetBaseSha ?? ''),
    createdAt: String(raw.createdAt ?? ''),
    updatedAt: String(raw.updatedAt ?? ''),
  }
}

function normalizeMergeRequestCommentAnchor(raw: unknown): MergeRequestCommentAnchor | null {
  if (!raw || typeof raw !== 'object') {
    return null
  }
  const record = raw as Record<string, unknown>
  return {
    headCommitSha: String(record.headCommitSha ?? record.commitSha ?? ''),
    filePath: String(record.filePath ?? ''),
    lineNumber: Number(record.lineNumber ?? record.line ?? 0),
    diffSide: String(record.diffSide ?? 'new') as DiffSide,
  }
}

function normalizeMergeRequestComment(raw: Record<string, unknown>): MergeRequestComment {
  const replies = Array.isArray(raw.replies)
    ? raw.replies.map(item => normalizeMergeRequestComment(item as Record<string, unknown>))
    : []
  return {
    id: normalizeId(raw.id),
    mergeRequestId: normalizeId(raw.mergeRequestId),
    authorUserId: normalizeId(raw.authorUserId),
    authorUsername: raw.authorUsername ? String(raw.authorUsername) : null,
    bodyMarkdown: String(raw.bodyMarkdown ?? raw.body ?? ''),
    createdAt: String(raw.createdAt ?? ''),
    updatedAt: String(raw.updatedAt ?? ''),
    editedAt: raw.editedAt ? String(raw.editedAt) : null,
    deletedAt: raw.deletedAt ? String(raw.deletedAt) : null,
    isDeleted: Boolean(raw.isDeleted),
    parentCommentId: raw.parentCommentId ? normalizeId(raw.parentCommentId) : null,
    isResolved: Boolean(raw.isResolved),
    isOutdated: Boolean(raw.isOutdated),
    replyCount: Number(raw.replyCount ?? replies.length),
    replies,
    anchor: normalizeMergeRequestCommentAnchor(raw.anchor),
  }
}

function normalizeMergeRequestChanges(raw: Record<string, unknown>): MergeRequestChanges {
  const files = Array.isArray(raw.files)
    ? raw.files.map((item) => {
      const record = item as Record<string, unknown>
      const hunks = Array.isArray(record.hunks)
        ? record.hunks.map((hunkRaw) => {
          const hunkRecord = hunkRaw as Record<string, unknown>
          const lines = Array.isArray(hunkRecord.lines)
            ? hunkRecord.lines.map((lineRaw) => {
              const lineRecord = lineRaw as Record<string, unknown>
              return {
                oldLineNumber: lineRecord.oldLineNumber == null ? null : Number(lineRecord.oldLineNumber),
                newLineNumber: lineRecord.newLineNumber == null ? null : Number(lineRecord.newLineNumber),
                type: String(lineRecord.type ?? 'context') as MergeRequestDiffLine['type'],
                content: String(lineRecord.content ?? ''),
              }
            })
            : []
          return {
            header: String(hunkRecord.header ?? ''),
            lines,
          }
        })
        : []
      return {
        filePath: String(record.filePath ?? ''),
        oldPath: record.oldPath == null ? null : String(record.oldPath),
        changeType: String(record.changeType ?? ''),
        hunks,
      }
    })
    : []
  return { files }
}

function normalizeMergeRequestCommit(raw: Record<string, unknown>): MergeRequestCommit {
  const sha = String(raw.sha ?? '')
  return {
    sha,
    shortSha: String(raw.shortSha ?? sha.slice(0, 8)),
    message: String(raw.message ?? ''),
    authorName: String(raw.authorName ?? ''),
    authoredAt: String(raw.authoredAt ?? raw.createdAt ?? ''),
  }
}

function normalizeRepositoryCommit(raw: Record<string, unknown>): RepositoryCommit {
  const sha = String(raw.sha ?? '')
  const diffFilesRaw = Array.isArray(raw.diffFiles) ? raw.diffFiles : []
  const rootFilesRaw = Array.isArray(raw.rootFiles) ? raw.rootFiles : []
  const statsRaw = raw.stats as Record<string, unknown> | null | undefined
  const parentsRaw = Array.isArray(raw.parents) ? raw.parents : []

  return {
    sha,
    shortSha: String(raw.shortSha ?? sha.slice(0, 8)),
    message: String(raw.message ?? ''),
    authorName: String(raw.authorName ?? ''),
    authoredAt: String(raw.authoredAt ?? ''),
    parents: parentsRaw.map((item) => {
      const parent = item as Record<string, unknown>
      const parentSha = String(parent.sha ?? '')
      return {
        sha: parentSha,
        shortSha: String(parent.shortSha ?? parentSha.slice(0, 8)),
      }
    }),
    stats: statsRaw
      ? {
          filesChanged: Number(statsRaw.filesChanged ?? 0),
          insertions: Number(statsRaw.insertions ?? 0),
          deletions: Number(statsRaw.deletions ?? 0),
        }
      : null,
    kind: String(raw.kind ?? 'diff') === 'root' ? 'root' : 'diff',
    diffFiles: normalizeMergeRequestChanges({ files: diffFilesRaw }).files,
    rootFiles: rootFilesRaw.map((item) => {
      const file = item as Record<string, unknown>
      return {
        path: String(file.path ?? ''),
        changeType: String(file.changeType ?? 'added'),
      }
    }),
    replicationLag: normalizeReplicationLag(raw.replicationLag),
  }
}

function normalizeMergeRequestDiscussionLink(raw: Record<string, unknown>): MergeRequestDiscussionLink {
  return {
    discussionNumber: Number(raw.discussionNumber ?? 0),
    relationshipType: String(raw.relationshipType ?? 'related') as MergeRequestLinkType,
    discussionTitle: raw.discussionTitle ? String(raw.discussionTitle) : null,
    discussionStatus: raw.discussionStatus ? String(raw.discussionStatus) : null,
  }
}

function normalizeDiscussionLinkedMergeRequest(raw: Record<string, unknown>): DiscussionLinkedMergeRequest {
  return {
    number: Number(raw.number ?? 0),
    title: String(raw.title ?? ''),
    status: normalizeMergeRequestStatus(raw.status),
    relationshipType: String(raw.relationshipType ?? 'related') as MergeRequestLinkType,
  }
}

function normalizeProtectedBranchRule(raw: Record<string, unknown>): ProtectedBranchRule {
  const pushRulesRaw = Array.isArray(raw.pushRules) ? raw.pushRules : []
  const extractedPushRules = extractPushRulesFromPayload(pushRulesRaw)
  const allowedPushRoles = Array.isArray(raw.allowedPushRoles)
    ? raw.allowedPushRoles.map(String)
    : decodeAllowedPushRoles(raw.allowedPushRoles)

  return {
    id: normalizeId(raw.id),
    pattern: String(raw.pattern ?? ''),
    blockDirectPush: Boolean(raw.blockDirectPush),
    allowedPushRoles,
    allowedPushUserIds: Array.isArray(raw.allowedPushUserIds)
      ? raw.allowedPushUserIds.map(item => normalizeId(item))
      : [],
    requiredApprovals: Number(raw.requiredApprovals ?? raw.requiredApprovalCount ?? 0),
    mergeRole: typeof raw.mergeRole === 'string'
      ? String(raw.mergeRole)
      : decodeMergeRole(raw.mergeRoleThreshold),
    forcePushPolicy: typeof raw.forcePushPolicy === 'string'
      ? String(raw.forcePushPolicy)
      : decodeForcePushPolicy(raw.forcePushPolicy),
    dismissApprovalsOnPush: Boolean(raw.dismissApprovalsOnPush ?? true),
    lockedMergeStrategy: raw.lockedMergeStrategy == null
      ? null
      : decodeLockedMergeStrategy(raw.lockedMergeStrategy),
    pushRules: {
      maxFileSizeBytes: raw.maxFileSizeBytes == null
        ? extractedPushRules.maxFileSizeBytes
        : Number(raw.maxFileSizeBytes),
      forbiddenPathGlobs: Array.isArray(raw.forbiddenPathGlobs)
        ? raw.forbiddenPathGlobs.map(String)
        : extractedPushRules.forbiddenPathGlobs,
      commitMessageRegex: raw.commitMessageRegex == null
        ? extractedPushRules.commitMessageRegex
        : String(raw.commitMessageRegex),
      requireDcoSignoff: raw.requireDcoSignoff == null
        ? extractedPushRules.requireDcoSignoff
        : Boolean(raw.requireDcoSignoff),
    },
  }
}

function decodeAllowedPushRoles(raw: unknown): string[] {
  const flags = Number(raw ?? 0)
  const roles: string[] = []
  if (flags & 1) {
    roles.push('Owner')
  }
  if (flags & 2) {
    roles.push('Admin')
  }
  if (flags & 4) {
    roles.push('Writer')
  }
  return roles
}

function encodeAllowedPushRoles(roles: string[]): number {
  let flags = 0
  for (const role of roles) {
    if (role === 'Owner') {
      flags |= 1
    }
    if (role === 'Admin') {
      flags |= 2
    }
    if (role === 'Writer') {
      flags |= 4
    }
  }
  return flags
}

const MERGE_ROLE_TO_THRESHOLD: Record<string, number> = {
  Reader: 1,
  Writer: 2,
  Admin: 3,
  Owner: 4,
}

const MERGE_THRESHOLD_TO_ROLE: Record<number, string> = {
  1: 'Reader',
  2: 'Writer',
  3: 'Admin',
  4: 'Owner',
}

function decodeMergeRole(raw: unknown): string {
  if (typeof raw === 'string') {
    return raw
  }
  return MERGE_THRESHOLD_TO_ROLE[Number(raw ?? 2)] ?? 'Writer'
}

function encodeMergeRole(role: string): number {
  return MERGE_ROLE_TO_THRESHOLD[role] ?? 2
}

const FORCE_PUSH_TO_VALUE: Record<string, number> = {
  DenyAll: 0,
  AllowAllowedPushers: 1,
  PlatformOnly: 2,
}

const FORCE_PUSH_FROM_VALUE: Record<number, string> = {
  0: 'DenyAll',
  1: 'AllowAllowedPushers',
  2: 'PlatformOnly',
}

function decodeForcePushPolicy(raw: unknown): string {
  if (typeof raw === 'string') {
    return raw
  }
  return FORCE_PUSH_FROM_VALUE[Number(raw ?? 0)] ?? 'DenyAll'
}

function encodeForcePushPolicy(policy: string): number {
  return FORCE_PUSH_TO_VALUE[policy] ?? 0
}

const LOCKED_MERGE_TO_VALUE: Record<string, number> = {
  MergeCommit: 0,
  Squash: 1,
  FastForward: 2,
}

function decodeLockedMergeStrategy(raw: unknown): string | null {
  if (raw == null) {
    return null
  }
  if (typeof raw === 'string') {
    return raw
  }
  const reverse: Record<number, string> = {
    0: 'MergeCommit',
    1: 'Squash',
    2: 'FastForward',
  }
  return reverse[Number(raw)] ?? null
}

function encodeLockedMergeStrategy(strategy: string | null | undefined): number | null {
  if (!strategy) {
    return null
  }
  return LOCKED_MERGE_TO_VALUE[strategy] ?? null
}

function extractPushRulesFromPayload(pushRules: unknown[]): ProtectedBranchRule['pushRules'] {
  const result: ProtectedBranchRule['pushRules'] = {
    maxFileSizeBytes: null,
    forbiddenPathGlobs: [],
    commitMessageRegex: null,
    requireDcoSignoff: false,
  }

  for (const item of pushRules) {
    if (!item || typeof item !== 'object') {
      continue
    }
    const record = item as Record<string, unknown>
    const ruleType = Number(record.ruleType ?? record.RuleType ?? -1)
    let config: Record<string, unknown> = {}
    try {
      config = JSON.parse(String(record.configJson ?? record.ConfigJson ?? '{}')) as Record<string, unknown>
    }
    catch {
      config = {}
    }

    if (ruleType === 0 && config.maxBytes != null) {
      result.maxFileSizeBytes = Number(config.maxBytes)
    }
    if (ruleType === 1 && Array.isArray(config.globs)) {
      result.forbiddenPathGlobs = config.globs.map(String)
    }
    if (ruleType === 2 && config.regex != null) {
      result.commitMessageRegex = String(config.regex)
    }
    if (ruleType === 3) {
      result.requireDcoSignoff = Boolean(config.required)
    }
  }

  return result
}

function buildPushRulesPayload(pushRules: ProtectedBranchRule['pushRules'] | undefined): Array<{ ruleType: number, configJson: string }> {
  const payload: Array<{ ruleType: number, configJson: string }> = []
  if (!pushRules) {
    return payload
  }
  if (pushRules.maxFileSizeBytes != null && pushRules.maxFileSizeBytes > 0) {
    payload.push({
      ruleType: 0,
      configJson: JSON.stringify({ maxBytes: pushRules.maxFileSizeBytes }),
    })
  }
  if (pushRules.forbiddenPathGlobs?.length) {
    payload.push({
      ruleType: 1,
      configJson: JSON.stringify({ globs: pushRules.forbiddenPathGlobs }),
    })
  }
  if (pushRules.commitMessageRegex) {
    payload.push({
      ruleType: 2,
      configJson: JSON.stringify({ regex: pushRules.commitMessageRegex }),
    })
  }
  if (pushRules.requireDcoSignoff) {
    payload.push({
      ruleType: 3,
      configJson: JSON.stringify({ required: true }),
    })
  }
  return payload
}

function serializeProtectedBranchRuleRequest(body: Partial<ProtectedBranchRule>): Record<string, unknown> {
  return {
    pattern: body.pattern ?? '',
    blockDirectPush: body.blockDirectPush ?? true,
    allowedPushRoles: encodeAllowedPushRoles(body.allowedPushRoles ?? ['Admin']),
    allowedPushUserIds: body.allowedPushUserIds ?? [],
    requireMergeRequest: true,
    requiredApprovalCount: body.requiredApprovals ?? 0,
    mergeRoleThreshold: encodeMergeRole(body.mergeRole ?? 'Writer'),
    forcePushPolicy: encodeForcePushPolicy(body.forcePushPolicy ?? 'DenyAll'),
    dismissApprovalsOnPush: body.dismissApprovalsOnPush ?? true,
    lockedMergeStrategy: encodeLockedMergeStrategy(body.lockedMergeStrategy),
    pushRules: buildPushRulesPayload(body.pushRules),
  }
}

const NOTIFICATION_EVENT_MAP: Record<number, NotificationEventType> = {
  0: 'NewComment',
  1: 'Mention',
  2: 'AssigneeChanged',
  3: 'Resolved',
  4: 'Dismissed',
  5: 'Reopened',
  6: 'SubThreadResolved',
  7: 'MergeRequestComment',
  8: 'MergeRequestMention',
  9: 'MergeRequestApproved',
  10: 'MergeRequestApprovalDismissed',
  11: 'MergeRequestMerged',
  12: 'MergeRequestClosed',
  13: 'MergeRequestReviewThreadResolved',
}

function normalizeNotificationEventType(raw: unknown): NotificationEventType {
  if (typeof raw === 'string') {
    return raw as NotificationEventType
  }
  return NOTIFICATION_EVENT_MAP[Number(raw)] ?? 'NewComment'
}

function normalizeNotification(raw: Record<string, unknown>): Notification {
  const readAt = raw.readAt ? String(raw.readAt) : null
  return {
    id: normalizeId(raw.id),
    userId: normalizeId(raw.userId),
    discussionId: raw.discussionId ? normalizeId(raw.discussionId) : null,
    mergeRequestId: raw.mergeRequestId ? normalizeId(raw.mergeRequestId) : null,
    repositoryId: raw.repositoryId ? normalizeId(raw.repositoryId) : null,
    discussionNumber: raw.discussionNumber == null ? null : Number(raw.discussionNumber),
    mergeRequestNumber: raw.mergeRequestNumber == null ? null : Number(raw.mergeRequestNumber),
    commentId: normalizeCommentId(raw.commentId),
    ownerSlug: String(raw.ownerSlug ?? raw.owner ?? ''),
    repositorySlug: String(raw.repositorySlug ?? raw.slug ?? ''),
    eventType: normalizeNotificationEventType(raw.eventType),
    message: String(raw.message ?? ''),
    actorUserId: raw.actorUserId ? normalizeId(raw.actorUserId) : null,
    createdAt: String(raw.createdAt ?? ''),
    readAt,
    isRead: Boolean(raw.isRead ?? readAt),
  }
}

function normalizeBlockedUser(raw: Record<string, unknown>): BlockedUser {
  return {
    userId: normalizeId(raw.userId),
    username: raw.username ? String(raw.username) : null,
    blockedByUserId: normalizeId(raw.blockedByUserId),
    blockedAt: String(raw.blockedAt ?? ''),
    reason: raw.reason ? String(raw.reason) : null,
  }
}

function discussionSlugPath(owner: string, slug: string): string {
  return `/repository/by-slug/${encodeURIComponent(owner)}/${encodeURIComponent(slug)}`
}

function mergeRequestSlugPath(owner: string, slug: string): string {
  return `${discussionSlugPath(owner, slug)}/merge-requests`
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
          userId?: string | { value: string }
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
            userId: normalizeId(result.data.userId),
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

      create: (slug: string, body: { repositoryName: string, isPrivate: boolean, organizationSlug?: string }) =>
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

    git: {
      getConfig: async () => {
        const result = await request<Record<string, unknown>>('/v1/git/config')
        if (!result.data) {
          return { ...result, data: null } satisfies ApiResult<GitConfig>
        }
        return {
          ...result,
          data: {
            gitBaseUrl: String(result.data.gitBaseUrl ?? ''),
            sshEnabled: Boolean(result.data.sshEnabled),
          },
        }
      },
    },

    accessTokens: {
      list: async () => {
        const result = await request<Record<string, unknown>[]>('/git-access-token')
        return {
          ...result,
          data: result.data?.map(normalizeGitAccessToken) ?? null,
        }
      },

      create: async (body: {
        name: string
        scope: string
        expiresAt?: string | null
        neverExpires?: boolean
      }) => {
        const result = await request<Record<string, unknown>>('/git-access-token', {
          method: 'POST',
          body: JSON.stringify(body),
        })
        if (!result.data) {
          return { ...result, data: null } satisfies ApiResult<CreateGitAccessTokenResult>
        }
        return {
          ...result,
          data: {
            id: normalizeId(result.data.id),
            token: String(result.data.token ?? ''),
            metadata: normalizeGitAccessToken(
              (result.data.metadata as Record<string, unknown> | undefined) ?? result.data,
            ),
          },
        }
      },

      delete: (id: string) =>
        request<null>(`/git-access-token/${id}`, { method: 'DELETE' }),
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

    repositoryContent: {
      getRefs: async (owner: string, slug: string) => {
        const result = await request<Record<string, unknown>>(
          `/repository/by-slug/${encodeURIComponent(owner)}/${encodeURIComponent(slug)}/content/refs`,
        )
        return {
          ...result,
          data: result.data ? normalizeContentRefs(result.data) : null,
        }
      },

      getTree: async (owner: string, slug: string, refName: string, path = '') => {
        const query = new URLSearchParams({ refName })
        if (path) {
          query.set('path', path)
        }
        const result = await request<Record<string, unknown>>(
          `/repository/by-slug/${encodeURIComponent(owner)}/${encodeURIComponent(slug)}/content/tree?${query.toString()}`,
        )
        return {
          ...result,
          data: result.data ? normalizeContentTree(result.data) : null,
        }
      },

      getBlob: async (owner: string, slug: string, refName: string, path: string) => {
        const query = new URLSearchParams({ refName, path })
        const result = await request<Record<string, unknown>>(
          `/repository/by-slug/${encodeURIComponent(owner)}/${encodeURIComponent(slug)}/content/blob?${query.toString()}`,
        )
        return {
          ...result,
          data: result.data ? normalizeContentBlob(result.data) : null,
        }
      },

      getReadme: async (owner: string, slug: string, refName: string) => {
        const query = new URLSearchParams({ refName })
        const result = await request<Record<string, unknown>>(
          `/repository/by-slug/${encodeURIComponent(owner)}/${encodeURIComponent(slug)}/content/readme?${query.toString()}`,
        )
        return {
          ...result,
          data: result.data ? normalizeContentReadme(result.data) : null,
        }
      },

      getRawBlobUrl: (owner: string, slug: string, refName: string, path: string) => {
        const query = new URLSearchParams({ refName, path })
        return `${baseUrl}/repository/by-slug/${encodeURIComponent(owner)}/${encodeURIComponent(slug)}/content/blob/raw?${query.toString()}`
      },

      getCommit: async (owner: string, slug: string, sha: string) => {
        const result = await request<Record<string, unknown>>(
          `/repository/by-slug/${encodeURIComponent(owner)}/${encodeURIComponent(slug)}/commits/${encodeURIComponent(sha)}`,
        )
        return {
          ...result,
          data: result.data ? normalizeRepositoryCommit(result.data) : null,
        }
      },
    },

    discussions: {
      list: async (
        owner: string,
        slug: string,
        params?: { status?: DiscussionStatus, assigneeUserId?: string, tagId?: string },
      ) => {
        const query = new URLSearchParams()
        if (params?.status) {
          query.set('status', params.status)
        }
        if (params?.assigneeUserId) {
          query.set('assigneeUserId', params.assigneeUserId)
        }
        if (params?.tagId) {
          query.set('tagId', params.tagId)
        }
        const suffix = query.size ? `?${query.toString()}` : ''
        const result = await request<Record<string, unknown>[]>(
          `${discussionSlugPath(owner, slug)}/discussions${suffix}`,
        )
        return {
          ...result,
          data: result.data?.map(normalizeDiscussion) ?? null,
        }
      },

      get: async (owner: string, slug: string, number: number, options?: { includeComments?: boolean }) => {
        const query = options?.includeComments ? '?include=comments' : ''
        const result = await request<Record<string, unknown>>(
          `${discussionSlugPath(owner, slug)}/discussions/${number}${query}`,
        )
        return {
          ...result,
          data: result.data ? normalizeDiscussion(result.data) : null,
        }
      },

      create: async (
        owner: string,
        slug: string,
        body: {
          title: string
          body?: string | null
          assigneeUserId?: string | null
          tagIds?: string[]
          anchor?: CommentAnchorInput | null
        },
      ) => {
        const result = await request<Record<string, unknown>>(
          `${discussionSlugPath(owner, slug)}/discussions`,
          { method: 'POST', body: JSON.stringify(body) },
        )
        return {
          ...result,
          data: result.data ? normalizeDiscussion(result.data) : null,
        }
      },

      update: async (
        owner: string,
        slug: string,
        number: number,
        body: {
          title?: string | null
          assigneeUserId?: string | null
          clearAssignee?: boolean
          tagIds?: string[] | null
        },
      ) => {
        const result = await request<Record<string, unknown>>(
          `${discussionSlugPath(owner, slug)}/discussions/${number}`,
          { method: 'PATCH', body: JSON.stringify(body) },
        )
        return {
          ...result,
          data: result.data ? normalizeDiscussion(result.data) : null,
        }
      },

      resolve: async (owner: string, slug: string, number: number) => {
        const result = await request<Record<string, unknown>>(
          `${discussionSlugPath(owner, slug)}/discussions/${number}/resolve`,
          { method: 'POST' },
        )
        return {
          ...result,
          data: result.data ? normalizeDiscussion(result.data) : null,
        }
      },

      dismiss: async (owner: string, slug: string, number: number) => {
        const result = await request<Record<string, unknown>>(
          `${discussionSlugPath(owner, slug)}/discussions/${number}/dismiss`,
          { method: 'POST' },
        )
        return {
          ...result,
          data: result.data ? normalizeDiscussion(result.data) : null,
        }
      },

      listComments: async (owner: string, slug: string, number: number) => {
        const result = await request<Record<string, unknown>[]>(
          `${discussionSlugPath(owner, slug)}/discussions/${number}/comments`,
        )
        return {
          ...result,
          data: result.data?.map(normalizeDiscussionComment) ?? null,
        }
      },

      createComment: async (
        owner: string,
        slug: string,
        number: number,
        body: {
          bodyMarkdown: string
          parentCommentId?: string | null
          anchor?: CommentAnchorInput | null
        },
      ) => {
        const result = await request<Record<string, unknown>>(
          `${discussionSlugPath(owner, slug)}/discussions/${number}/comments`,
          { method: 'POST', body: JSON.stringify(body) },
        )
        return {
          ...result,
          data: result.data ? normalizeDiscussionComment(result.data) : null,
        }
      },

      resolveSubThread: async (owner: string, slug: string, commentId: string) => {
        const result = await request<Record<string, unknown>>(
          `${discussionSlugPath(owner, slug)}/discussions/comments/${commentId}/resolve`,
          { method: 'POST' },
        )
        return {
          ...result,
          data: result.data ? normalizeDiscussionComment(result.data) : null,
        }
      },

      unresolveSubThread: async (owner: string, slug: string, commentId: string) => {
        const result = await request<Record<string, unknown>>(
          `${discussionSlugPath(owner, slug)}/discussions/comments/${commentId}/unresolve`,
          { method: 'POST' },
        )
        return {
          ...result,
          data: result.data ? normalizeDiscussionComment(result.data) : null,
        }
      },

      listLinkedMergeRequests: async (owner: string, slug: string, number: number) => {
        const result = await request<Record<string, unknown>[]>(
          `${discussionSlugPath(owner, slug)}/discussions/${number}/linked-merge-requests`,
        )
        return {
          ...result,
          data: result.data?.map(normalizeDiscussionLinkedMergeRequest) ?? null,
        }
      },

      tags: {
        list: async (owner: string, slug: string) => {
          const result = await request<Record<string, unknown>[]>(
            `${discussionSlugPath(owner, slug)}/tags`,
          )
          return {
            ...result,
            data: result.data?.map(normalizeRepositoryTag) ?? null,
          }
        },

        create: async (owner: string, slug: string, body: { name: string, color?: string | null }) => {
          const result = await request<Record<string, unknown>>(
            `${discussionSlugPath(owner, slug)}/tags`,
            { method: 'POST', body: JSON.stringify(body) },
          )
          return {
            ...result,
            data: result.data ? normalizeRepositoryTag(result.data) : null,
          }
        },

        delete: (owner: string, slug: string, tagId: string) =>
          request<null>(`${discussionSlugPath(owner, slug)}/tags/${tagId}`, { method: 'DELETE' }),
      },

      blockedUsers: {
        list: async (owner: string, slug: string) => {
          const result = await request<Record<string, unknown>[]>(
            `${discussionSlugPath(owner, slug)}/blocked-users`,
          )
          return {
            ...result,
            data: result.data?.map(normalizeBlockedUser) ?? null,
          }
        },

        block: async (
          owner: string,
          slug: string,
          body: { userId: string, reason?: string | null },
        ) => {
          const result = await request<Record<string, unknown>>(
            `${discussionSlugPath(owner, slug)}/blocked-users`,
            { method: 'POST', body: JSON.stringify(body) },
          )
          return {
            ...result,
            data: result.data ? normalizeBlockedUser(result.data) : null,
          }
        },

        unblock: (owner: string, slug: string, userId: string) =>
          request<null>(
            `${discussionSlugPath(owner, slug)}/blocked-users/${userId}`,
            { method: 'DELETE' },
          ),
      },

      notifications: {
        list: async (params?: { unreadOnly?: boolean }) => {
          const query = params?.unreadOnly ? '?unreadOnly=true' : ''
          const result = await request<Record<string, unknown>[]>(`/notifications${query}`)
          return {
            ...result,
            data: result.data?.map(normalizeNotification) ?? null,
          }
        },

        markRead: (notificationId: string) =>
          request<null>(`/notifications/${notificationId}/read`, { method: 'POST' }),
      },
    },

    mergeRequests: {
      list: async (owner: string, slug: string) => {
        const result = await request<Record<string, unknown>[]>(mergeRequestSlugPath(owner, slug))
        return {
          ...result,
          data: result.data?.map(normalizeMergeRequest) ?? null,
        }
      },

      get: async (owner: string, slug: string, number: number) => {
        const result = await request<Record<string, unknown>>(`${mergeRequestSlugPath(owner, slug)}/${number}`)
        return {
          ...result,
          data: result.data ? normalizeMergeRequest(result.data) : null,
        }
      },

      create: async (
        owner: string,
        slug: string,
        body: {
          title: string
          body?: string | null
          sourceRef: string
          targetRef: string
          isDraft?: boolean
        },
      ) => {
        const result = await request<Record<string, unknown>>(
          mergeRequestSlugPath(owner, slug),
          { method: 'POST', body: JSON.stringify(body) },
        )
        return {
          ...result,
          data: result.data ? normalizeMergeRequest(result.data) : null,
        }
      },

      listComments: async (owner: string, slug: string, number: number, params?: { type?: 'overview' | 'review' }) => {
        const query = params?.type ? `?type=${params.type}` : ''
        const result = await request<Record<string, unknown>[]>(
          `${mergeRequestSlugPath(owner, slug)}/${number}/comments${query}`,
        )
        return {
          ...result,
          data: result.data?.map(normalizeMergeRequestComment) ?? null,
        }
      },

      createComment: async (
        owner: string,
        slug: string,
        number: number,
        body: {
          bodyMarkdown: string
          parentCommentId?: string | null
          anchor?: MergeRequestCommentAnchor | null
        },
      ) => {
        const result = await request<Record<string, unknown>>(
          `${mergeRequestSlugPath(owner, slug)}/${number}/comments`,
          { method: 'POST', body: JSON.stringify(body) },
        )
        return {
          ...result,
          data: result.data ? normalizeMergeRequestComment(result.data) : null,
        }
      },

      updateComment: async (
        owner: string,
        slug: string,
        number: number,
        commentId: string,
        body: { bodyMarkdown: string },
      ) => {
        const result = await request<Record<string, unknown>>(
          `${mergeRequestSlugPath(owner, slug)}/${number}/comments/${commentId}`,
          { method: 'PATCH', body: JSON.stringify(body) },
        )
        return {
          ...result,
          data: result.data ? normalizeMergeRequestComment(result.data) : null,
        }
      },

      deleteComment: (owner: string, slug: string, number: number, commentId: string) =>
        request<null>(`${mergeRequestSlugPath(owner, slug)}/${number}/comments/${commentId}`, { method: 'DELETE' }),

      resolveComment: async (owner: string, slug: string, number: number, commentId: string) => {
        const result = await request<Record<string, unknown>>(
          `${mergeRequestSlugPath(owner, slug)}/${number}/comments/${commentId}/resolve`,
          { method: 'POST' },
        )
        return {
          ...result,
          data: result.data ? normalizeMergeRequestComment(result.data) : null,
        }
      },

      unresolveComment: async (owner: string, slug: string, number: number, commentId: string) => {
        const result = await request<Record<string, unknown>>(
          `${mergeRequestSlugPath(owner, slug)}/${number}/comments/${commentId}/unresolve`,
          { method: 'POST' },
        )
        return {
          ...result,
          data: result.data ? normalizeMergeRequestComment(result.data) : null,
        }
      },

      getChanges: async (owner: string, slug: string, number: number) => {
        const result = await request<Record<string, unknown>>(`${mergeRequestSlugPath(owner, slug)}/${number}/changes`)
        return {
          ...result,
          data: result.data ? normalizeMergeRequestChanges(result.data) : null,
        }
      },

      listCommits: async (owner: string, slug: string, number: number) => {
        const result = await request<Record<string, unknown>[]>(
          `${mergeRequestSlugPath(owner, slug)}/${number}/commits`,
        )
        return {
          ...result,
          data: result.data?.map(normalizeMergeRequestCommit) ?? null,
        }
      },

      getBranchAheadSummary: async (owner: string, slug: string, refName: string) => {
        const result = await request<Record<string, unknown>>(
          `${mergeRequestSlugPath(owner, slug)}/branch-ahead-summary?ref=${encodeURIComponent(refName)}`,
        )
        if (!result.data) {
          return { ...result, data: null } satisfies ApiResult<BranchAheadSummary>
        }
        return {
          ...result,
          data: {
            aheadCount: Number(result.data.aheadCount ?? 0),
            defaultRef: result.data.defaultRef ? String(result.data.defaultRef) : null,
            hasActiveMergeRequest: Boolean(result.data.hasActiveMergeRequest),
          },
        }
      },

      listDiscussionLinks: async (owner: string, slug: string, number: number) => {
        const result = await request<Record<string, unknown>[]>(
          `${mergeRequestSlugPath(owner, slug)}/${number}/discussion-links`,
        )
        return {
          ...result,
          data: result.data?.map(normalizeMergeRequestDiscussionLink) ?? null,
        }
      },

      createDiscussionLink: async (
        owner: string,
        slug: string,
        number: number,
        body: { discussionNumber: number, relationshipType: MergeRequestLinkType },
      ) => {
        const result = await request<Record<string, unknown>>(
          `${mergeRequestSlugPath(owner, slug)}/${number}/discussion-links`,
          { method: 'POST', body: JSON.stringify(body) },
        )
        return {
          ...result,
          data: result.data ? normalizeMergeRequestDiscussionLink(result.data) : null,
        }
      },

      deleteDiscussionLink: (
        owner: string,
        slug: string,
        number: number,
        discussionNumber: number,
        relationshipType: MergeRequestLinkType,
      ) =>
        request<null>(
          `${mergeRequestSlugPath(owner, slug)}/${number}/discussion-links/${discussionNumber}?relationshipType=${relationshipType}`,
          { method: 'DELETE' },
        ),
    },

    repositorySettings: {
      getDefaultBranch: async (owner: string, slug: string) => {
        const repoResult = await request<Record<string, unknown>>(`/repository/by-slug/${encodeURIComponent(owner)}/${encodeURIComponent(slug)}`)
        if (!repoResult.data) {
          return { ...repoResult, data: null } satisfies ApiResult<{ defaultBranchName: string | null }>
        }
        const repositoryId = normalizeId(repoResult.data.id)
        const result = await request<Record<string, unknown>>(
          `/repository/${repositoryId}/settings/default-branch`,
        )
        if (!result.data) {
          return { ...result, data: null } satisfies ApiResult<{ defaultBranchName: string | null }>
        }
        return {
          ...result,
          data: {
            defaultBranchName: result.data.defaultBranchName
              ? String(result.data.defaultBranchName)
              : null,
          },
        }
      },

      updateDefaultBranch: async (owner: string, slug: string, body: { defaultBranchName: string }) => {
        const repoResult = await request<Record<string, unknown>>(`/repository/by-slug/${encodeURIComponent(owner)}/${encodeURIComponent(slug)}`)
        if (!repoResult.data) {
          return { ...repoResult, data: null } satisfies ApiResult<{ defaultBranchName: string | null }>
        }
        const repositoryId = normalizeId(repoResult.data.id)
        return request<{ defaultBranchName: string | null }>(
          `/repository/${repositoryId}/settings/default-branch`,
          { method: 'PATCH', body: JSON.stringify(body) },
        )
      },

      listProtectedBranchRules: async (owner: string, slug: string) => {
        const repoResult = await request<Record<string, unknown>>(`/repository/by-slug/${encodeURIComponent(owner)}/${encodeURIComponent(slug)}`)
        if (!repoResult.data) {
          return { ...repoResult, data: null } satisfies ApiResult<ProtectedBranchRule[]>
        }
        const repositoryId = normalizeId(repoResult.data.id)
        const result = await request<Record<string, unknown>[]>(
          `/repository/${repositoryId}/protected-branch-rules`,
        )
        return {
          ...result,
          data: result.data?.map(normalizeProtectedBranchRule) ?? null,
        }
      },

      createProtectedBranchRule: async (owner: string, slug: string, body: Partial<ProtectedBranchRule>) => {
        const repoResult = await request<Record<string, unknown>>(`/repository/by-slug/${encodeURIComponent(owner)}/${encodeURIComponent(slug)}`)
        if (!repoResult.data) {
          return { ...repoResult, data: null } satisfies ApiResult<ProtectedBranchRule>
        }
        const repositoryId = normalizeId(repoResult.data.id)
        const result = await request<unknown>(
          `/repository/${repositoryId}/protected-branch-rules`,
          { method: 'POST', body: JSON.stringify(serializeProtectedBranchRuleRequest(body)) },
        )
        if (result.error) {
          return { ...result, data: null } satisfies ApiResult<ProtectedBranchRule>
        }
        const listResult = await request<Record<string, unknown>[]>(
          `/repository/${repositoryId}/protected-branch-rules`,
        )
        const createdId = typeof result.data === 'string'
          ? normalizeId(result.data)
          : normalizeId((result.data as Record<string, unknown> | null)?.value ?? result.data)
        const created = listResult.data?.find(item => normalizeId(item.id) === createdId)
        return {
          ...result,
          data: created ? normalizeProtectedBranchRule(created) : null,
        }
      },

      updateProtectedBranchRule: async (owner: string, slug: string, ruleId: string, body: Partial<ProtectedBranchRule>) => {
        const repoResult = await request<Record<string, unknown>>(`/repository/by-slug/${encodeURIComponent(owner)}/${encodeURIComponent(slug)}`)
        if (!repoResult.data) {
          return { ...repoResult, data: null } satisfies ApiResult<ProtectedBranchRule>
        }
        const repositoryId = normalizeId(repoResult.data.id)
        const result = await request<null>(
          `/repository/${repositoryId}/protected-branch-rules/${ruleId}`,
          { method: 'PUT', body: JSON.stringify(serializeProtectedBranchRuleRequest(body)) },
        )
        if (result.error) {
          return { ...result, data: null } satisfies ApiResult<ProtectedBranchRule>
        }
        const listResult = await request<Record<string, unknown>[]>(
          `/repository/${repositoryId}/protected-branch-rules`,
        )
        const updated = listResult.data?.find(item => normalizeId(item.id) === ruleId)
        return {
          ...result,
          data: updated ? normalizeProtectedBranchRule(updated) : null,
        }
      },

      deleteProtectedBranchRule: async (owner: string, slug: string, ruleId: string) => {
        const repoResult = await request<Record<string, unknown>>(`/repository/by-slug/${encodeURIComponent(owner)}/${encodeURIComponent(slug)}`)
        if (!repoResult.data) {
          return { ...repoResult, data: null } satisfies ApiResult<null>
        }
        const repositoryId = normalizeId(repoResult.data.id)
        return request<null>(
          `/repository/${repositoryId}/protected-branch-rules/${ruleId}`,
          { method: 'DELETE' },
        )
      },
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
      replication: {
        listStorageNodeSummary: () =>
          request<AdminStorageNodeReplicationSummaryDto[]>('/admin/storage-nodes/replication-summary'),
        listRepositories: (params?: {
          page?: number
          pageSize?: number
          sort?: string
          search?: string
          attention?: string
        }) => {
          const query = new URLSearchParams()
          if (params?.page) query.set('page', String(params.page))
          if (params?.pageSize) query.set('pageSize', String(params.pageSize))
          if (params?.sort) query.set('sort', params.sort)
          if (params?.search) query.set('search', params.search)
          if (params?.attention) query.set('attention', params.attention)
          const suffix = query.size ? `?${query.toString()}` : ''
          return request<AdminRepositoryReplicationListResponse>(`/admin/repositories${suffix}`)
        },
        getRepository: (repositoryId: string) =>
          request<AdminRepositoryReplicationDetailDto>(`/admin/repositories/${repositoryId}/replication`),
      },
    },
  }
}

export type ApiClient = ReturnType<typeof createApi>

/** Exported for unit tests. */
export { normalizeDiscussion, normalizeDiscussionComment }
