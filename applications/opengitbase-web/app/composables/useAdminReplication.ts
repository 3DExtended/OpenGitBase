export const ADMIN_REPLICATION_POLL_MS = 30_000

export type ReplicationAttentionFilter =
  | 'all'
  | 'backfilling'
  | 'degraded'
  | 'lagging'
  | 'no-quorum'

export type ReplicationSort = 'severity' | 'name' | 'lag' | 'state'

export function repositoryNeedsAttention(summary: {
  replicationState: string
  writeQuorumAvailable: boolean
  maxWatermarkLag: number
  replicaCount: number
}): boolean {
  return summary.replicationState !== 'Rf3Healthy'
    || !summary.writeQuorumAvailable
    || summary.maxWatermarkLag > 0
    || summary.replicaCount < 3
}

export function replicationStateBadgeColor(state: string): 'success' | 'warning' | 'error' | 'neutral' {
  switch (state) {
    case 'Rf3Healthy':
      return 'success'
    case 'Rf1Backfilling':
      return 'warning'
    case 'Degraded':
    case 'Promoting':
      return 'error'
    default:
      return 'neutral'
  }
}

export function provisioningProgressPercent(replicaCount: number): number {
  return Math.min(100, Math.round((replicaCount / 3) * 100))
}

export function syncProgressPercent(maxWatermarkLag: number, primaryWatermark: number): number {
  if (primaryWatermark <= 0 && maxWatermarkLag <= 0) {
    return 100
  }

  if (maxWatermarkLag <= 0) {
    return 100
  }

  const denominator = Math.max(primaryWatermark, maxWatermarkLag, 1)
  return Math.max(0, Math.min(100, Math.round((1 - maxWatermarkLag / denominator) * 100)))
}

export function rollupReplicationStates(
  items: Array<{ replicationState: string }>,
): Record<string, number> {
  return items.reduce<Record<string, number>>((counts, item) => {
    counts[item.replicationState] = (counts[item.replicationState] ?? 0) + 1
    return counts
  }, {})
}

export function useAdminReplicationPoll(callback: () => void | Promise<void>) {
  let timer: ReturnType<typeof setInterval> | undefined

  onMounted(() => {
    void callback()
    timer = setInterval(() => {
      void callback()
    }, ADMIN_REPLICATION_POLL_MS)
  })

  onUnmounted(() => {
    if (timer) {
      clearInterval(timer)
    }
  })
}
