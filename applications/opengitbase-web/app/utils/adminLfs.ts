import type { AdminRepositoryLfsStatusDto } from './api'

/** Share of the objects a node holds, rounded down so a node that still misses a copy never shows 100. */
export function lfsCopyPercent(copies: number, objectCount: number): number {
  if (objectCount <= 0) {
    return 100
  }
  return Math.floor((Math.min(copies, objectCount) / objectCount) * 100)
}

export interface LfsRestoreProgress {
  nodeId: string
  restored: number
  total: number
  percent: number
}

/**
 * The LFS restore onto the primary (after a cold recovery or a promotion), or null when the primary already holds
 * every object. Copies missing elsewhere are ordinary repair work, not a restore.
 */
export function lfsRestoreProgress(status: AdminRepositoryLfsStatusDto): LfsRestoreProgress | null {
  const primary = status.nodes.find(node => node.isPrimary)
  if (!primary || primary.missing <= 0) {
    return null
  }
  return {
    nodeId: primary.nodeId || primary.storageNodeId,
    restored: primary.copies,
    total: status.objectCount,
    percent: lfsCopyPercent(primary.copies, status.objectCount),
  }
}
